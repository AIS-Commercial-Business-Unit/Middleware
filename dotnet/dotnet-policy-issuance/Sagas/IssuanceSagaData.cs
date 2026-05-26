using NServiceBus;

namespace dotnet_policy_issuance.Sagas;

public sealed class IssuanceSagaData : ContainSagaData
{
    public string IssuanceId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public string Status { get; set; } = "Initiated";
    public int PolicyTypeCode { get; set; }
    public int PolicyTypeSubCode { get; set; }
    public string SubmittingChannel { get; set; } = "DirectRequest";
    public DateTimeOffset RequestedAt { get; set; }
    public string? AccountServiceRequestNumber { get; set; }
    public List<string> PolicyNumbers { get; set; } = [];
    public string? TargetPas { get; set; }
    public bool BillingComplete { get; set; }
    public bool CustomerUpdateComplete { get; set; }
    public string? FailureReason { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? BatchId { get; set; }
    public string? RecordId { get; set; }
    public int PasRetryCount { get; set; }
}
