using NServiceBus;

namespace Middleware.Contracts.Events;

/// <summary>
/// Published by PolicyIssuanceAndLifecycleManagement when an IssuePolicy command is received.
/// Platform.Compliance subscribes and runs economic sanctions screening.
/// EDA: PolicyLifecycle announces that issuance has begun — it does not command Compliance to check.
/// </summary>
public sealed class PolicyIssuanceInitiatedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
