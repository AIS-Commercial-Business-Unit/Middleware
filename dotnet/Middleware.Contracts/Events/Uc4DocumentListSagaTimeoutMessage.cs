namespace Middleware.Contracts.Events;

public sealed class Uc4DocumentListSagaTimeoutMessage
{
    public string RequestId { get; set; } = string.Empty;
}
