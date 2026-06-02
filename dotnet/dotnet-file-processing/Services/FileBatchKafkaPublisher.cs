using System.Text.Json;
using Azure.Identity;
using Confluent.Kafka;

namespace dotnet_file_processing.Services;

public sealed class FileBatchKafkaPublisher : IDisposable
{
    private readonly IProducer<Null, string> _producer;

    public FileBatchKafkaPublisher(IConfiguration configuration)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
        var eventHubsNamespace = configuration["Azure:EventHubsNamespace"];

        var producerConfig = new ProducerConfig { BootstrapServers = bootstrapServers };

        if (!string.IsNullOrEmpty(eventHubsNamespace))
        {
            producerConfig.BootstrapServers = $"{eventHubsNamespace}.servicebus.windows.net:9093";
            producerConfig.SecurityProtocol = SecurityProtocol.SaslSsl;
            producerConfig.SaslMechanism = SaslMechanism.OAuthBearer;

            _producer = new ProducerBuilder<Null, string>(producerConfig)
                .SetOAuthBearerTokenRefreshHandler((client, _) =>
                {
                    var credential = new DefaultAzureCredential();
                    var token = credential.GetToken(
                        new Azure.Core.TokenRequestContext(new[] { "https://eventhubs.azure.net/.default" }));
                    client.OAuthBearerSetToken(token.Token, token.ExpiresOn.ToUnixTimeMilliseconds(), "");
                })
                .Build();
        }
        else
        {
            _producer = new ProducerBuilder<Null, string>(producerConfig).Build();
        }
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
