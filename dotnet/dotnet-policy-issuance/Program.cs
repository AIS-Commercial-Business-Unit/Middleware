using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Serilog;
using Serilog.Formatting.Json;
using dotnet_policy_issuance.Behaviors;
using dotnet_policy_issuance.Handlers;
using dotnet_policy_issuance.Infrastructure;
using dotnet_policy_issuance.Sagas;
using Middleware.Contracts.Commands;
using NServiceBus;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-policy-issuance";

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateLogger();

builder.Host.UseSerilog();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton<IssuePolicyCommandHandler>();
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing =>
    {
        tracing
            .AddAspNetCoreInstrumentation()
            .AddHttpClientInstrumentation()
            .AddOtlpExporter();
        var appInsightsCs = builder.Configuration["ApplicationInsights:ConnectionString"];
        if (!string.IsNullOrEmpty(appInsightsCs))
            tracing.AddAzureMonitorTraceExporter(o => o.ConnectionString = appInsightsCs);
    });

var mongoClient = new MongoDB.Driver.MongoClient(builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017");
var repository = new MongoIssuanceSagaRepository(mongoClient, "dotnet_policy_issuance_db", "issuance_sagas");
builder.Services.AddSingleton<MongoDB.Driver.IMongoClient>(mongoClient);
builder.Services.AddSingleton<IIssuanceSagaRecordRepository>(repository);
PolicyIssuanceRuntime.Repository = repository;

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-policy-issuance");
endpointConfiguration.EnableInstallers();
endpointConfiguration.EnableOutbox();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var loggerFactory = LoggerFactory.Create(logging =>
{
    logging.AddSerilog(Log.Logger, dispose: false);
});
endpointConfiguration.Pipeline.Register(
    new EDAFlowIncomingBehavior(loggerFactory.CreateLogger<EDAFlowIncomingBehavior>()),
    "EDA flow logging — incoming");
endpointConfiguration.Pipeline.Register(
    new EDAFlowOutgoingBehavior(loggerFactory.CreateLogger<EDAFlowOutgoingBehavior>()),
    "EDA flow logging — outgoing");

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(sqlConnectionString);
transport.Transactions(TransportTransactionMode.ReceiveOnly);
var routing = transport.Routing();
routing.RouteToEndpoint(typeof(PublishNotificationIntentCommand), "dotnet-platform-notification");

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(sqlConnectionString));
persistence.TablePrefix("nsb");

var endpointInstance = await NServiceBus.Endpoint.Start(endpointConfiguration).ConfigureAwait(false);
builder.Services.AddSingleton<IMessageSession>(endpointInstance);

var app = builder.Build();
PolicyIssuanceRuntime.Logger = app.Services.GetService<ILogger<IssuanceSaga>>();
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
app.Lifetime.ApplicationStopping.Register(() => endpointInstance.Stop().GetAwaiter().GetResult());
await app.RunAsync().ConfigureAwait(false);
