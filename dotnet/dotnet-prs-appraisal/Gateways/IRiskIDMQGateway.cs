namespace dotnet_prs_appraisal.Gateways;

// ⚠️ DEMO GAP: IBM MQ inbound gateway — stubbed as HTTP for demo
// Production: Real IBM MQ consumer using NServiceBus or Camel adapter
// REPLACE_ME_IBM_MQ_ADAPTER — implementation choice: C#, Java, or Logic App
public interface IRiskIDMQGateway
{
    /// <summary>
    /// Receive an inbound RiskID status update message.
    /// Production: pulls from IBM MQ MQSC queue.
    /// Demo: called via HTTP POST /api/appraisal/status-update.
    /// </summary>
    Task<RiskIDReceiveResult> ReceiveStatusUpdateAsync(string rawPayload, CancellationToken ct = default);
}

public sealed class RiskIDReceiveResult
{
    public string InspectionId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string InspectionTypeCode { get; set; } = string.Empty;
    public bool IsValid { get; set; }

    // ⚠️ DEMO GAP: Actual RiskID MQ payload schema — REPLACE_ME_RISKID_MQ_SCHEMA
    public string? RawPayload { get; set; }
}
