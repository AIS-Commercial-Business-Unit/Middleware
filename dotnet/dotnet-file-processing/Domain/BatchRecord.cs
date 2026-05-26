using MongoDB.Bson.Serialization.Attributes;

namespace dotnet_file_processing.Domain;

public sealed class BatchRecord
{
    [BsonId]
    public string RecordId { get; set; } = string.Empty;
    public string BatchId { get; set; } = string.Empty;
    public int SequenceNumber { get; set; }
    public string RawContent { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public string? ProcessorResult { get; set; }
    public DateTimeOffset? ProcessedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
