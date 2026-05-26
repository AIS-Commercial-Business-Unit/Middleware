using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class RequestComplianceCheckCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public DateTimeOffset RequestedAt { get; set; } = DateTimeOffset.UtcNow;
}
