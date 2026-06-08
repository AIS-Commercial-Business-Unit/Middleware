using dotnet_file_processing.Domain;
using dotnet_file_processing.Services;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Handlers;

public sealed class FileBatchStartedEventHandler : IHandleMessages<FileBatchStartedEvent>
{
    private readonly FileProcessingStore _store;
    private readonly ILogger<FileBatchStartedEventHandler> _logger;

    public FileBatchStartedEventHandler(
        FileProcessingStore store,
        ILogger<FileBatchStartedEventHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task Handle(FileBatchStartedEvent message, IMessageHandlerContext context)
    {
        using var _ = LogContext.PushProperty("BatchId", message.BatchId);

        var batch = new FileBatch
        {
            BatchId = message.BatchId,
            FileName = message.FileName,
            TotalRecords = message.RecordCount,
            PercentComplete = 0,
            Status = "Processing",
            ReceivedAt = message.StartedAt
        };

        await _store.UpsertBatchAsync(batch, context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "File batch record created — batchId={BatchId} fileName={FileName} totalRecords={TotalRecords}",
            message.BatchId,
            message.FileName,
            message.RecordCount);
    }
}
