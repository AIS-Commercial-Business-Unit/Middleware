using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using NServiceBus;
using dotnet_customer_identity.Infrastructure;
using dotnet_customer_identity.Handlers;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-customer-identity";

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

CustomerIdentityRuntime.AccountServiceUrl = builder.Configuration["ExternalServices:AccountServiceUrl"]
    ?? "http://erm7x1-account-stub:9005/api/account";
CustomerIdentityRuntime.CustomerServiceUrl = builder.Configuration["ExternalServices:CustomerServiceUrl"]
    ?? "http://crm40x1-customer-stub:9006/api/customer";

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-customer-identity");
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

CustomerIdentityRuntime.Logger = app.Services.GetService<ILogger<GetOrCreateAccountServiceRecordHandler>>();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);
