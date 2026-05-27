using NServiceBus;

namespace Middleware.Contracts.Events;

/// <summary>
/// Published by PolicyIssuanceAndLifecycleManagement after compliance clears.
/// CustomerIdentityAndRelationshipManagement subscribes and retrieves or creates the account service record.
/// EDA: PolicyLifecycle announces it needs the account record — it does not command CustomerIdentity.
/// </summary>
public sealed class AccountLookupRequestedEvent : IEvent
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
