using NServiceBus;

namespace dotnet_prs_appraisal.Sagas;

public sealed class MainframeDocumentAggregatorSagaData : ContainSagaData
{
    public string RequestId { get; set; } = string.Empty;

    public string DocumentKey { get; set; } = string.Empty;

    public string AccumulatedChunksJson { get; set; } = string.Empty;

    public bool IsFinalChunkReceived { get; set; }

    public DateTimeOffset StartedAt { get; set; }
}
