using dotnet_file_processing.Services;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Sagas;

public sealed class FileProcessingSaga :
    Saga<FileProcessingSagaData>,
    IAmStartedByMessages<FileBatchStartedEvent>,
    IHandleTimeouts<FileProcessingSagaTimeoutMessage>
{
    private const int CheckIntervalSeconds = 5;
    private const int MaxChecks = 360; // 30-minute upper bound at 5s intervals

    private readonly IFileBatchProgressManager _progressManager;
    private readonly FileProcessingStore _store;
    private readonly ILogger<FileProcessingSaga> _logger;

    public FileProcessingSaga(
        IFileBatchProgressManager progressManager,
        FileProcessingStore store,
        ILogger<FileProcessingSaga> logger)
    {
        _progressManager = progressManager;
        _store = store;
        _logger = logger;
    }

    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<FileProcessingSagaData> mapper)
    {
        mapper.MapSaga(data => data.BatchId)
            .ToMessage<FileBatchStartedEvent>(m => m.BatchId);
    }

    public async Task Handle(FileBatchStartedEvent message, IMessageHandlerContext context)
    {
        Data ??= new FileProcessingSagaData();
        Data.BatchId = message.BatchId;
        Data.FileName = message.FileName;
        Data.TotalRecords = message.RecordCount;
        Data.StartedAt = message.StartedAt;

        using var _ = LogContext.PushProperty("BatchId", Data.BatchId);

        _logger.LogInformation(
            "File processing saga started — batchId={BatchId} fileName={FileName} totalRecords={TotalRecords}",
            Data.BatchId, Data.FileName, Data.TotalRecords);

        if (Data.TotalRecords == 0)
        {
            await PublishCompletedAsync(context).ConfigureAwait(false);
            return;
        }

        await RequestTimeout(context, TimeSpan.FromSeconds(CheckIntervalSeconds),
            new FileProcessingSagaTimeoutMessage { BatchId = message.BatchId, CheckCount = 1 })
            .ConfigureAwait(false);
    }

    public async Task Timeout(FileProcessingSagaTimeoutMessage state, IMessageHandlerContext context)
    {
        using var _ = LogContext.PushProperty("BatchId", Data.BatchId);

        var progress = await _progressManager.GetProgressAsync(Data.BatchId, context.CancellationToken)
            .ConfigureAwait(false);

        Data.ProcessedRecords = progress.ProcessedRecords;
        Data.SucceededRecords = progress.SucceededRecords;
        Data.FailedRecords = progress.FailedRecords;

        var percentComplete = Data.TotalRecords > 0
            ? (double)progress.ProcessedRecords / Data.TotalRecords * 100
            : 0;

        _logger.LogInformation(
            "File processing saga status check — batchId={BatchId} processed={Processed} total={Total} percent={Percent:F1}% check={Check}",
            Data.BatchId, progress.ProcessedRecords, Data.TotalRecords, percentComplete, state.CheckCount);

        await UpdateBatchProgressAsync(progress, percentComplete, context.CancellationToken).ConfigureAwait(false);

        if (progress.IsComplete(Data.TotalRecords))
        {
            await PublishCompletedAsync(context).ConfigureAwait(false);
            return;
        }

        if (state.CheckCount >= MaxChecks)
        {
            _logger.LogWarning(
                "File processing saga timed out — batchId={BatchId} processed={Processed}/{Total}",
                Data.BatchId, progress.ProcessedRecords, Data.TotalRecords);
            await PublishTimedOutAsync(context).ConfigureAwait(false);
            return;
        }

        await RequestTimeout(context, TimeSpan.FromSeconds(CheckIntervalSeconds),
            new FileProcessingSagaTimeoutMessage { BatchId = Data.BatchId, CheckCount = state.CheckCount + 1 })
            .ConfigureAwait(false);
    }

    private async Task PublishCompletedAsync(IMessageHandlerContext context)
    {
        var batch = await _store.GetBatchAsync(Data.BatchId, context.CancellationToken).ConfigureAwait(false);
        if (batch is not null)
        {
            batch.Status = Data.FailedRecords == 0 ? "Completed" : "PartialFailure";
            batch.ProcessingCompletedAt = DateTimeOffset.UtcNow;
            batch.PercentComplete = 100;
            batch.ProcessedRecords = Data.ProcessedRecords;
            batch.SucceededRecords = Data.SucceededRecords;
            batch.FailedRecords = Data.FailedRecords;
            await _store.UpsertBatchAsync(batch, context.CancellationToken).ConfigureAwait(false);
        }

        await context.Publish(new FileBatchCompletedEvent
        {
            BatchId = Data.BatchId,
            FileName = Data.FileName,
            RecordCount = Data.TotalRecords,
            SucceededRecords = Data.SucceededRecords,
            FailedRecords = Data.FailedRecords,
            CompletedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        _logger.LogInformation(
            "File processing saga completed — batchId={BatchId} succeeded={Succeeded} failed={Failed}",
            Data.BatchId, Data.SucceededRecords, Data.FailedRecords);

        MarkAsComplete();
    }

    private async Task PublishTimedOutAsync(IMessageHandlerContext context)
    {
        var batch = await _store.GetBatchAsync(Data.BatchId, context.CancellationToken).ConfigureAwait(false);
        if (batch is not null)
        {
            batch.Status = "TimedOut";
            batch.ProcessingCompletedAt = DateTimeOffset.UtcNow;
            batch.PercentComplete = Data.TotalRecords > 0
                ? (double)Data.ProcessedRecords / Data.TotalRecords * 100
                : 0;
            batch.ProcessedRecords = Data.ProcessedRecords;
            batch.SucceededRecords = Data.SucceededRecords;
            batch.FailedRecords = Data.FailedRecords;
            await _store.UpsertBatchAsync(batch, context.CancellationToken).ConfigureAwait(false);
        }

        await context.Publish(new FileBatchCompletedEvent
        {
            BatchId = Data.BatchId,
            FileName = Data.FileName,
            RecordCount = Data.TotalRecords,
            SucceededRecords = Data.SucceededRecords,
            FailedRecords = Data.FailedRecords,
            CompletedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);

        MarkAsComplete();
    }

    private async Task UpdateBatchProgressAsync(BatchProcessingStatus progress, double percentComplete, CancellationToken cancellationToken)
    {
        var batch = await _store.GetBatchAsync(Data.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null) return;

        batch.ProcessedRecords = progress.ProcessedRecords;
        batch.SucceededRecords = progress.SucceededRecords;
        batch.FailedRecords = progress.FailedRecords;
        batch.PercentComplete = percentComplete;
        batch.Status = "Processing";
        await _store.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
    }
}
