using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class AssociateBillingAccountCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string AccountServiceRequestNumber { get; set; } = string.Empty;
    public string BillingChannel { get; set; } = "DirectBill";
}
