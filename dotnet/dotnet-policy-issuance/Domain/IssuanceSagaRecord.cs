using MongoDB.Bson.Serialization.Attributes;

namespace dotnet_policy_issuance.Domain;

public sealed class IssuanceSagaRecord
{
    [BsonId]
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string? AccountServiceRequestNumber { get; set; }
    public List<string>? PolicyNumbers { get; set; }
    public string Status { get; set; } = "Initiated";
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public string? TargetPas { get; set; }
    public int PasRetryCount { get; set; }
    public bool BillingComplete { get; set; }
    public bool CustomerUpdateComplete { get; set; }
    public string? FailureReason { get; set; }
    [BsonElement("requestedAt")]
    public DateTimeOffset RequestedAt { get; set; }
    [BsonElement("completedAt")]
    public DateTimeOffset? CompletedAt { get; set; }
    public string SubmittingChannel { get; set; } = "DirectRequest";
    public string? RecordId { get; set; }
    public string? BatchId { get; set; }
}
