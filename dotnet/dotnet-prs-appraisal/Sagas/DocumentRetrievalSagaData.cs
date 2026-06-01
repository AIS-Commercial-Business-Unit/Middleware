using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentRetrievalSagaData : ContainSagaData
{
    public string RequestId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }

    public bool AtWorkPending { get; set; }

    public bool AtWorkDone { get; set; }

    public string AtWorkContent { get; set; } = string.Empty;

    public string AtWorkMimeType { get; set; } = string.Empty;
}
