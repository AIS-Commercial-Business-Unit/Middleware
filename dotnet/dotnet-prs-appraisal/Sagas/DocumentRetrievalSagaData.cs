using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class DocumentRetrievalSagaData : ContainSagaData
{
    public string RequestId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string SourceSystem { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
}
