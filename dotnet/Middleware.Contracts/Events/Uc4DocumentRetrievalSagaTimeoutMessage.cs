namespace Middleware.Contracts.Events;

public sealed class Uc4DocumentRetrievalSagaTimeoutMessage
{
    public string RequestId { get; set; } = string.Empty;
}
