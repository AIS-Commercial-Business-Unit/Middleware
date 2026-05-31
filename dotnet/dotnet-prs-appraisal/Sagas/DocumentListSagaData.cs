using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentListSagaData : ContainSagaData
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string AtWorkDocumentsJson { get; set; } = string.Empty;

    public string MainframeDocumentsJson { get; set; } = string.Empty;

    public bool AtWorkDone { get; set; }

    public bool MainframeDone { get; set; }

    public DateTimeOffset StartedAt { get; set; }
}
