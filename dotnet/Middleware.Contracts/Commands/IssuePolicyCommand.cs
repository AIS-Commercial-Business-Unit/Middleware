using Middleware.Contracts.Models;
using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class IssuePolicyCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<PolicyItem> Policies { get; set; } = [];
    public string SubmittingChannel { get; set; } = "DirectRequest";
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? BatchId { get; set; }
    public string? RecordId { get; set; }
}
