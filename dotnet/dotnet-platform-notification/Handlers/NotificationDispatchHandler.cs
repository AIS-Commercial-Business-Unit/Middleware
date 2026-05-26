using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_platform_notification.Infrastructure;

namespace dotnet_platform_notification.Handlers;

public sealed class NotificationDispatchHandler : IHandleMessages<PublishNotificationIntentCommand>
{
    public async Task Handle(PublishNotificationIntentCommand message, IMessageHandlerContext context)
    {
        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            NotificationRuntime.Logger?.LogInformation(
                "NotificationDispatch started — issuanceId={IssuanceId} accountId={AccountId} " +
                "notificationType={NotificationType} message={NotificationMessage}",
                message.IssuanceId, message.AccountId, message.NotificationType,
                message.Message?.Length > 100 ? message.Message[..100] + "…" : message.Message);
        }

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            NotificationRuntime.Logger?.LogInformation(
                "Notification DISPATCHED — issuanceId={IssuanceId} notificationType={NotificationType}",
                message.IssuanceId, message.NotificationType);
        }

        await context.Publish(new NotificationDispatchedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            NotificationType = message.NotificationType,
            DispatchedAt = DateTimeOffset.UtcNow
        }).ConfigureAwait(false);
    }
}
