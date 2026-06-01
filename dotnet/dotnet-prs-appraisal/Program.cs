using dotnet_prs_appraisal.Behaviors;
using dotnet_prs_appraisal.Infrastructure;
using Microsoft.Data.SqlClient;
using Middleware.Contracts.Commands;
using MongoDB.Driver;
using NServiceBus;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Azure.Monitor.OpenTelemetry.Exporter;
using Serilog;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateLogger();

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddHealthChecks();

var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-prs-appraisal";
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

// ── UC4 services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IArtemisAdapter, ArtemisAdapter>();
builder.Services.AddHostedService<ArtemisListReplyListener>();
builder.Services.AddHostedService<ArtemisDocumentReplyListener>();

// ── MongoDB (document request polling) ───────────────────────────────────────
var mongoConnectionString = builder.Configuration.GetConnectionString("MongoDB")
    ?? "mongodb://localhost:27017";
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConnectionString));
builder.Services.AddSingleton<IDocumentListRequestRepository>(sp =>
    new MongoDocumentListRequestRepository(sp.GetRequiredService<IMongoClient>(), "prs_appraisal"));
builder.Services.AddSingleton<IDocumentRetrievalRequestRepository>(sp =>
    new MongoDocumentRetrievalRequestRepository(sp.GetRequiredService<IMongoClient>(), "prs_appraisal"));

builder.Host.UseNServiceBus(_ =>
{
    var endpointConfiguration = new EndpointConfiguration("dotnet-prs-appraisal");
    var connectionString = builder.Configuration.GetConnectionString("NServiceBus")
        ?? throw new InvalidOperationException("ConnectionStrings:NServiceBus is required.");

    var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
    transport.ConnectionString(connectionString);
    transport.DefaultSchema("dbo");

    var routing = transport.Routing();
    routing.RouteToEndpoint(typeof(GetAppraisalDocumentListCommand), "dotnet-prs-appraisal");
    routing.RouteToEndpoint(typeof(RetrieveAppraisalDocumentCommand), "dotnet-prs-appraisal");
    routing.RouteToEndpoint(typeof(StartMainframeDocumentAggregationCommand), "dotnet-prs-appraisal");

    var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
    persistence.SqlDialect<SqlDialect.MsSqlServer>();
    persistence.ConnectionBuilder(() => new SqlConnection(connectionString));

    endpointConfiguration.EnableInstallers();
    endpointConfiguration.UseSerialization<SystemJsonSerializer>();
    endpointConfiguration.SendFailedMessagesTo("error");
    endpointConfiguration.AuditProcessedMessagesTo("audit");
    endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowHandlerInvokeBehavior), "Logs EDA flow events at handler invocation for subscriber fan-out visibility.");
    endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowOutgoingBehavior), "Logs outgoing EDA flow events.");

    return endpointConfiguration;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
