using NServiceBus;

namespace Middleware.Contracts.Commands;

public sealed class PublishNotificationIntentCommand : ICommand
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string NotificationType { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
