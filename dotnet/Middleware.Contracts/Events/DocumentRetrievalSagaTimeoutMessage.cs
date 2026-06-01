namespace Middleware.Contracts.Events;

public sealed class DocumentRetrievalSagaTimeoutMessage
{
    public string RequestId { get; set; } = string.Empty;
}
