namespace Middleware.Contracts.Events;

public sealed class FileProcessingSagaTimeoutMessage
{
    public string BatchId { get; set; } = string.Empty;
    public int CheckCount { get; set; }
}
