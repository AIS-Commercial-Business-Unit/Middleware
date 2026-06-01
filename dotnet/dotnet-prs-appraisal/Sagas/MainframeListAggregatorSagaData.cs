using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class MainframeListAggregatorSagaData : ContainSagaData
{
    public string RequestId { get; set; } = string.Empty;

    public string PolicyNumber { get; set; } = string.Empty;

    public DateTimeOffset StartedAt { get; set; }
}
