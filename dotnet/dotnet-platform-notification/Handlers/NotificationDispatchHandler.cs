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
                "NotificationDispatch started — issuanceId={IssuanceId} type={NotificationType}",
                message.IssuanceId,
                message.NotificationType);
        }

        var dispatchedEvent = new NotificationDispatchedEvent
        {
            IssuanceId = message.IssuanceId,
            AccountId = message.AccountId,
            NotificationType = message.NotificationType,
            DispatchedAt = DateTimeOffset.UtcNow
        };

        await context.Publish(dispatchedEvent).ConfigureAwait(false);

        using (LogContext.PushProperty("issuanceId", message.IssuanceId))
        {
            NotificationRuntime.Logger?.LogInformation(
                "Notification DISPATCHED — issuanceId={IssuanceId}",
                dispatchedEvent.IssuanceId);
        }
    }
}
