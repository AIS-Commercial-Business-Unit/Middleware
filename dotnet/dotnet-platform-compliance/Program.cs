using Microsoft.Data.SqlClient;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Formatting.Json;
using NServiceBus;
using dotnet_platform_compliance.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
var serviceName = builder.Configuration["OTEL_SERVICE_NAME"] ?? "dotnet-platform-compliance";

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

ComplianceRuntime.ComplianceCheckUrl = builder.Configuration["ExternalServices:ComplianceCheckUrl"]
    ?? "http://rsk3x3-compliance-stub:9004/api/check";

var sqlConnectionString = builder.Configuration.GetConnectionString("NServiceBus")
    ?? "Server=localhost;Database=middleware_nsb;User=sa;Password=AIS_Middleware_2024!;TrustServerCertificate=True";

var endpointConfiguration = new EndpointConfiguration("dotnet-platform-compliance");
endpointConfiguration.EnableInstallers();
endpointConfiguration.EnableOutbox();
endpointConfiguration.UseSerialization<SystemJsonSerializer>();

var transport = endpointConfiguration.UseTransport<SqlServerTransport>();
transport.ConnectionString(sqlConnectionString);

var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(sqlConnectionString));
persistence.TablePrefix("nsb");

var endpointInstance = await NServiceBus.Endpoint.Start(endpointConfiguration).ConfigureAwait(false);
var app = builder.Build();
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
app.Lifetime.ApplicationStopping.Register(() => endpointInstance.Stop().GetAwaiter().GetResult());
await app.RunAsync().ConfigureAwait(false);
