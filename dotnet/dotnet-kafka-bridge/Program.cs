using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using NServiceBus;
using Middleware.Platform;
using Serilog;
using Serilog.Formatting.Json;
using dotnet_kafka_bridge.Infrastructure;
using dotnet_kafka_bridge.Handlers;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-kafka-bridge";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var kafkaBootstrapServers = builder.Configuration["Kafka:BootstrapServers"] ?? "localhost:9092";
KafkaBridgeRuntime.Producer = new ProducerBuilder<Null, string>(new ProducerConfig { BootstrapServers = kafkaBootstrapServers }).Build();

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-kafka-bridge");
endpointConfiguration.EnableInstallers();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(sqlConnectionString);
transport.Transactions(TransportTransactionMode.ReceiveOnly);

endpointConfiguration.ApplyParticularPlatformDefaults(builder.Configuration);
builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();

KafkaBridgeRuntime.Logger = app.Services.GetService<ILogger<PolicyIssuedEventHandler>>();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
app.Lifetime.ApplicationStopping.Register(() =>
{
    KafkaBridgeRuntime.Producer?.Flush(TimeSpan.FromSeconds(5));
    KafkaBridgeRuntime.Producer?.Dispose();
});
await app.RunAsync().ConfigureAwait(false);
