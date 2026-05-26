using Microsoft.Extensions.Logging;

namespace dotnet_platform_notification.Infrastructure;

public static class NotificationRuntime
{
    public static ILogger? Logger { get; set; }
}
