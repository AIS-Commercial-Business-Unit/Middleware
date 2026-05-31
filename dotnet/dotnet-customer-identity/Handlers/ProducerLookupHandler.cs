using dotnet_customer_identity.Infrastructure;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_customer_identity.Handlers;

/// <summary>
/// ProducerLookupHandler — UC4 Appraisal Documents.
/// Subscribes to ProducerLookupRequestedEvent, retrieves producer cross-reference
/// from the CustomerDB gateway stub, and publishes ProducerCrossReferenceRetrievedEvent.
///
/// ⚠️ DEMO GAP: Real Customer DB schema for producer cross-reference unknown.
/// REPLACE_ME_CUSTOMER_DB_SCHEMA — current implementation uses in-memory seed data.
/// </summary>
public sealed class ProducerLookupHandler : IHandleMessages<ProducerLookupRequestedEvent>
{
    // ⚠️ DEMO GAP: Real CustomerDB schema needed — REPLACE_ME_CUSTOMER_DB_SCHEMA
    // Keyed by policy number → (producerCode, uwControlCode)
    private static readonly Dictionary<string, (string ProducerCode, string UWControlCode)> MockProducerData =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["POL-12345"] = ("REPLACE_ME_PRODUCER_001", "UA"),
            ["POL-12346"] = ("REPLACE_ME_PRODUCER_002", "UST"),
            ["POL-12347"] = ("REPLACE_ME_PRODUCER_003", "UA"),
            ["POL-12348"] = ("REPLACE_ME_PRODUCER_004", "UA"),
            ["POL-12349"] = ("REPLACE_ME_PRODUCER_005", "UA"),
        };

    public async Task Handle(ProducerLookupRequestedEvent message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("appraisalId", message.AppraisalId))
        using (LogContext.PushProperty("correlationId", message.CorrelationId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA subscriber] dotnet-customer-identity received ProducerLookupRequestedEvent — " +
                "appraisalId={AppraisalId} policyNumber={PolicyNumber}",
                message.AppraisalId, message.PolicyNumber);
        }

        // ⚠️ DEMO GAP: Producer cross-reference lookup is in-memory mock.
        // Production: call Customer DB stored procedure or REST API.
        // REPLACE_ME_CUSTOMER_DB_XREF_QUERY
        MockProducerData.TryGetValue(message.PolicyNumber, out var entry);
        var producerCode = entry.ProducerCode ?? $"REPLACE_ME_PRODUCER_NOT_FOUND_{message.PolicyNumber}";
        var uwControlCode = entry.UWControlCode ?? "UA"; // ⚠️ DEMO GAP: Default to UA when not found

        if (string.IsNullOrEmpty(entry.ProducerCode))
        {
            CustomerIdentityRuntime.Logger?.LogWarning(
                "⚠️ DEMO GAP: ProducerLookupHandler — no seed data for policyNumber={PolicyNumber}. " +
                "Returning mock values. REPLACE_ME_CUSTOMER_DB_LOOKUP",
                message.PolicyNumber);
        }

        using (LogContext.PushProperty("appraisalId", message.AppraisalId))
        {
            CustomerIdentityRuntime.Logger?.LogInformation(
                "[EDA publish] dotnet-customer-identity publishing ProducerCrossReferenceRetrievedEvent — " +
                "appraisalId={AppraisalId} producerCode={ProducerCode} uwControlCode={UWControlCode}",
                message.AppraisalId, producerCode, uwControlCode);
        }

        await context.Publish(new ProducerCrossReferenceRetrievedEvent
        {
            AppraisalId = message.AppraisalId,
            PolicyNumber = message.PolicyNumber,
            ProducerCode = producerCode,
            UWControlCode = uwControlCode,
            CorrelationId = message.CorrelationId,
            RetrievedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}
