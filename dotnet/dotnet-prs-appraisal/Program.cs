using dotnet_prs_appraisal.Behaviors;
using Middleware.Contracts.Commands;
using dotnet_prs_appraisal.Infrastructure;
using Microsoft.Data.SqlClient;
using MongoDB.Driver;
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

var nServiceBusConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? throw new InvalidOperationException("ConnectionStrings:NServiceBus is required.");

// ── UC4 services ──────────────────────────────────────────────────────────────
builder.Services.AddSingleton<IArtemisAdapter, ArtemisAdapter>();
builder.Services.AddSingleton<IAccumulatorRepository>(_ => new AccumulatorRepository(nServiceBusConnectionString));
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

var endpointConfiguration = new EndpointConfiguration("dotnet-prs-appraisal");

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(nServiceBusConnectionString);
transport.DefaultSchema("dbo");
transport.Transactions(TransportTransactionMode.SendsAtomicWithReceive);

var routing = transport.Routing();
routing.RouteToEndpoint(typeof(GetAppraisalDocumentListCommand), "dotnet-prs-appraisal");
routing.RouteToEndpoint(typeof(RetrieveAppraisalDocumentCommand), "dotnet-prs-appraisal");

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(nServiceBusConnectionString));

endpointConfiguration.EnableInstallers();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();
endpointConfiguration.SendFailedMessagesTo("error");
endpointConfiguration.AuditProcessedMessagesTo("audit");
endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowHandlerInvokeBehavior), "Logs EDA flow events at handler invocation for subscriber fan-out visibility.");
endpointConfiguration.Pipeline.Register(typeof(AppraisalEDAFlowOutgoingBehavior), "Logs outgoing EDA flow events.");

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();
await app.Services.GetRequiredService<IAccumulatorRepository>().EnsureCreatedAsync();

app.UseSerilogRequestLogging();

app.MapHealthChecks("/health");
app.MapControllers();
app.Run();
