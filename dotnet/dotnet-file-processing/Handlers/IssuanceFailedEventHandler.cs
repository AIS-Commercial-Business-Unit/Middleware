using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Handlers;

public sealed class IssuanceFailedEventHandler : IHandleMessages<IssuanceFailedEvent>
{
    private readonly ILogger<IssuanceFailedEventHandler> _logger;

    public IssuanceFailedEventHandler(ILogger<IssuanceFailedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(IssuanceFailedEvent message, IMessageHandlerContext context)
    {
        if (string.IsNullOrEmpty(message.BatchId))
            return; // Not part of a batch — ignore

        using var _1 = LogContext.PushProperty("BatchId", message.BatchId);
        using var _2 = LogContext.PushProperty("IssuanceId", message.IssuanceId);

        _logger.LogWarning(
            "Issuance failed for batch item — batchId={BatchId} recordId={RecordId} issuanceId={IssuanceId} reason={Reason}",
            message.BatchId,
            message.RecordId,
            message.IssuanceId,
            message.Reason);

        await context.Publish(new BatchItemFailedEvent
        {
            BatchId = message.BatchId,
            RecordId = message.RecordId ?? string.Empty,
            IssuanceId = message.IssuanceId,
            ErrorMessage = message.Reason,
            ProcessedAt = message.FailedAt
        }).ConfigureAwait(false);
    }
}
