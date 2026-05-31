using dotnet_prs_appraisal.Behaviors;
using dotnet_prs_appraisal.Infrastructure;
using Microsoft.Data.SqlClient;
using Middleware.Contracts.Commands;
using NServiceBus;
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

// ── UC4 services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<ICallbackRegistry, CallbackRegistry>();
builder.Services.AddSingleton<IArtemisAdapter, ArtemisAdapter>();
builder.Services.AddHostedService<ArtemisListReplyListener>();
builder.Services.AddHostedService<ArtemisDocumentReplyListener>();

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
    routing.RouteToEndpoint(typeof(StartMainframeListAggregationCommand), "dotnet-prs-appraisal");
    routing.RouteToEndpoint(typeof(StartMainframeDocumentAggregationCommand), "dotnet-prs-appraisal");

    var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
    persistence.SqlDialect<SqlDialect.MsSqlServer>();
    persistence.ConnectionBuilder(() => new SqlConnection(connectionString));

    endpointConfiguration.EnableInstallers();
    endpointConfiguration.UseSerialization<SystemJsonSerializer>();
    endpointConfiguration.SendFailedMessagesTo("error");
    endpointConfiguration.AuditProcessedMessagesTo("audit");
    endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowIncomingBehavior), "Logs incoming EDA flow events.");
    endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowOutgoingBehavior), "Logs outgoing EDA flow events.");

    return endpointConfiguration;
});

var app = builder.Build();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
