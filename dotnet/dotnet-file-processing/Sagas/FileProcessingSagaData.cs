using NServiceBus;

namespace dotnet_file_processing.Sagas;

public sealed class FileProcessingSagaData : ContainSagaData
{
    public string BatchId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public int TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SucceededRecords { get; set; }
    public int FailedRecords { get; set; }
    public DateTimeOffset StartedAt { get; set; }
}
