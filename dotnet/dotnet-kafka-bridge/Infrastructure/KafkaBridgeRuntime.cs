using Microsoft.Extensions.Logging;
using System.Text.Json;
using Confluent.Kafka;

namespace dotnet_kafka_bridge.Infrastructure;

public static class KafkaBridgeRuntime
{
    public static IProducer<Null, string>? Producer { get; set; }
    public static ILogger? Logger { get; set; }

    // camelCase matches Java Jackson defaults — required for cross-stack Kafka consumers
    private static readonly JsonSerializerOptions KafkaJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static Task PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken = default)
    {
        if (Producer is null)
        {
            throw new InvalidOperationException("Kafka producer has not been configured.");
        }

        return Producer.ProduceAsync(topic, new Message<Null, string> { Value = JsonSerializer.Serialize(payload, KafkaJsonOptions) }, cancellationToken);
    }
}
