using System.Text.Json;
using Confluent.Kafka;

namespace dotnet_file_processing.Services;

public sealed class FileBatchKafkaPublisher : IDisposable
{
    private readonly IProducer<Null, string> _producer;

    public FileBatchKafkaPublisher(IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        _producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = bootstrapServers }).Build();
    }

    public Task PublishAsync<T>(string topic, T payload, CancellationToken cancellationToken)
    {
        return _producer.ProduceAsync(
            topic,
            new Message<Null, string> { Value = JsonSerializer.Serialize(payload) },
            cancellationToken);
    }

    public void Dispose()
    {
        _producer.Flush(TimeSpan.FromSeconds(5));
        _producer.Dispose();
    }
}
