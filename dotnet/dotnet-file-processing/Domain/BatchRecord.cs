using MongoDB.Bson.Serialization.Attributes;

namespace dotnet_file_processing.Domain;

public sealed class BatchRecord
{
    [BsonId]
    public string RecordId { get; set; } = string.Empty;
    [BsonElement("batchId")]
    public string BatchId { get; set; } = string.Empty;
    [BsonElement("sequenceNumber")]
    public int SequenceNumber { get; set; }
    public string RawContent { get; set; } = string.Empty;
    [BsonElement("status")]
    public string Status { get; set; } = "Pending";
    public int RetryCount { get; set; }
    public string? ProcessorResult { get; set; }
    [BsonElement("processedAt")]
    public DateTimeOffset? ProcessedAt { get; set; }
    public string CorrelationId { get; set; } = string.Empty;

    // Parsed renewal fields stored at intake to avoid re-parsing during fan-out
    public string PolicyNumber { get; set; } = string.Empty;
    public string ExpirationDate { get; set; } = string.Empty;
    public string InsuredName { get; set; } = string.Empty;
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public decimal PremiumAmount { get; set; }
    public string ProducerCode { get; set; } = string.Empty;
    public string BillingType { get; set; } = "DirectBill";
    public string AccountId { get; set; } = string.Empty;
}
