namespace Middleware.Contracts.Events;

public sealed class DocumentListSagaTimeoutMessage
{
    public string RequestId { get; set; } = string.Empty;
}
