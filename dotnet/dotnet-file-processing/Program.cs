using Microsoft.Data.SqlClient;
using Middleware.Contracts.Commands;
using MongoDB.Driver;
using NServiceBus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using dotnet_file_processing.Services;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-file-processing";

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

var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017");
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<FileProcessingStore>();
builder.Services.AddSingleton<IFileBatchProgressManager, FileBatchProgressManager>();
builder.Services.AddHostedService<FilePollingService>();

var nsbConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? throw new InvalidOperationException("ConnectionStrings:NServiceBus is required.");

var endpointConfiguration = new EndpointConfiguration("dotnet-file-processing");

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(nsbConnectionString);
transport.DefaultSchema("dbo");
transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

var routing = transport.Routing();
routing.RouteToEndpoint(typeof(IssuePolicyCommand), "dotnet-policy-issuance");

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(nsbConnectionString));

endpointConfiguration.EnableInstallers();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var recoverability = endpointConfiguration.Recoverability();
recoverability.Immediate(immediate => immediate.NumberOfRetries(3));
recoverability.Delayed(delayed =>
{
    delayed.NumberOfRetries(2);
    delayed.TimeIncrease(TimeSpan.FromSeconds(10));
});

endpointConfiguration.SendFailedMessagesTo("error");
endpointConfiguration.AuditProcessedMessagesTo("audit");

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);
