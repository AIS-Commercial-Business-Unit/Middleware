using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using NServiceBus;
using Middleware.Platform;
using dotnet_platform_integration.Infrastructure;
using dotnet_platform_integration.Handlers;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-platform-integration";

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

PasGatewayRuntime.DuckCreekCommercialUrl = builder.Configuration["ExternalServices:DuckCreekCommercialUrl"]
    ?? "http://duckcreek-commercial-stub:9001/api/issuance";
PasGatewayRuntime.DuckCreekPersonalUrl = builder.Configuration["ExternalServices:DuckCreekPersonalUrl"]
    ?? "http://duckcreek-personal-stub:9002/api/issuance";
PasGatewayRuntime.ForeFrontUrl = builder.Configuration["ExternalServices:ForeFrontUrl"]
    ?? "http://forefront-stub:9003/api/issuance";

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-platform-integration");
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

endpointConfiguration.ApplyParticularPlatformDefaults(builder.Configuration);
builder.Services.AddNServiceBusEndpoint(endpointConfiguration);

var app = builder.Build();

PasGatewayRuntime.Logger = app.Services.GetService<ILogger<PasGatewayHandler>>();

app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);

