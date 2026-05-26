using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class UpdateCustomerRecordCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public List<string> PolicyNumbers { get; set; } = [];
    public string TargetPas { get; set; } = string.Empty;
}
