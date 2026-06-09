using MongoDB.Bson.Serialization.Attributes;

namespace dotnet_file_processing.Domain;

public sealed class FileBatch
{
    [BsonId]
    public string BatchId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DropZoneName { get; set; } = "AutomatedRenewal";
    public string FileType { get; set; } = "csv";
    public string FileLocationReference { get; set; } = string.Empty;
    public long FileSizeBytes { get; set; }
    public int? TotalRecords { get; set; }
    public int ProcessedRecords { get; set; }
    public int SucceededRecords { get; set; }
    public int FailedRecords { get; set; }
    public double PercentComplete { get; set; }
    [BsonElement("status")]
    public string Status { get; set; } = "Received";
    [BsonElement("receivedAt")]
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    [BsonElement("parsingCompletedAt")]
    public DateTimeOffset? ParsingCompletedAt { get; set; }
    [BsonElement("processingCompletedAt")]
    public DateTimeOffset? ProcessingCompletedAt { get; set; }
    public string ProcessingMode { get; set; } = "AutomatedRenewal";
}
