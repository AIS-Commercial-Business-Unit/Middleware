using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using NServiceBus;
using dotnet_billing_finance.Infrastructure;
using dotnet_billing_finance.Handlers;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-billing-finance";

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

BillingRuntime.BillingUrl = builder.Configuration["ExternalServices:BillingUrl"]
    ?? "http://crm19x1-billing-stub:9007/api/billing";

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-billing-finance");
endpointConfiguration.EnableInstallers();
endpointConfiguration.EnableOutbox();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(sqlConnectionString);
transport.Transactions(TransportTransactionMode.ReceiveOnly);

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(sqlConnectionString));
persistence.TablePrefix("nsb");

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();

BillingRuntime.Logger = app.Services.GetService<ILogger<BillingAssociationHandler>>();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);
