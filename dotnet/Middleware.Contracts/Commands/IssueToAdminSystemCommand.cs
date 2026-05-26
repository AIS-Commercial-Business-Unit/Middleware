using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class IssueToAdminSystemCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
}
