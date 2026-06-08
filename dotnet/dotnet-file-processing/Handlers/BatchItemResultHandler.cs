using System.Text.Json;
using dotnet_file_processing.Domain;
using dotnet_file_processing.Services;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Handlers;

public sealed class BatchItemResultHandler :
    IHandleMessages<BatchItemSucceededEvent>,
    IHandleMessages<BatchItemFailedEvent>
{
    private readonly FileProcessingStore _store;
    private readonly ILogger<BatchItemResultHandler> _logger;

    public BatchItemResultHandler(
        FileProcessingStore store,
        ILogger<BatchItemResultHandler> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task Handle(BatchItemSucceededEvent message, IMessageHandlerContext context)
    {
        using var _1 = LogContext.PushProperty("BatchId", message.BatchId);
        using var _2 = LogContext.PushProperty("RecordId", message.RecordId);

        var record = new BatchRecord
        {
            RecordId = message.RecordId,
            BatchId = message.BatchId,
            CorrelationId = message.IssuanceId,
            Status = "Succeeded",
            ProcessedAt = message.ProcessedAt,
            ProcessorResult = JsonSerializer.Serialize(new { issuanceId = message.IssuanceId })
        };

        await _store.UpsertRecordAsync(record, context.CancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Batch item result saved — batchId={BatchId} recordId={RecordId} status=Succeeded",
            message.BatchId,
            message.RecordId);
    }

    public async Task Handle(BatchItemFailedEvent message, IMessageHandlerContext context)
    {
        using var _1 = LogContext.PushProperty("BatchId", message.BatchId);
        using var _2 = LogContext.PushProperty("RecordId", message.RecordId);

        var record = new BatchRecord
        {
            RecordId = message.RecordId,
            BatchId = message.BatchId,
            CorrelationId = message.IssuanceId,
            Status = "Failed",
            ProcessedAt = message.ProcessedAt,
            ProcessorResult = JsonSerializer.Serialize(new { error = message.ErrorMessage, issuanceId = message.IssuanceId })
        };

        await _store.UpsertRecordAsync(record, context.CancellationToken).ConfigureAwait(false);

        _logger.LogWarning(
            "Batch item result saved — batchId={BatchId} recordId={RecordId} status=Failed error={Error}",
            message.BatchId,
            message.RecordId,
            message.ErrorMessage);
    }
}
