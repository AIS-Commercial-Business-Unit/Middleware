using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Handlers;

public sealed class PolicyIssuedEventHandler : IHandleMessages<PolicyIssuedEvent>
{
    private readonly ILogger<PolicyIssuedEventHandler> _logger;

    public PolicyIssuedEventHandler(ILogger<PolicyIssuedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(PolicyIssuedEvent message, IMessageHandlerContext context)
    {
        if (string.IsNullOrEmpty(message.BatchId))
            return; // Not part of a batch — ignore

        using var _1 = LogContext.PushProperty("BatchId", message.BatchId);
        using var _2 = LogContext.PushProperty("IssuanceId", message.IssuanceId);

        _logger.LogInformation(
            "Policy issued for batch item — batchId={BatchId} recordId={RecordId} issuanceId={IssuanceId}",
            message.BatchId,
            message.RecordId,
            message.IssuanceId);

        await context.Publish(new BatchItemSucceededEvent
        {
            BatchId = message.BatchId,
            RecordId = message.RecordId ?? string.Empty,
            IssuanceId = message.IssuanceId,
            ProcessedAt = message.CompletedAt
        }).ConfigureAwait(false);
    }
}
