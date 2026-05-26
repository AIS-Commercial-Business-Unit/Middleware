using MongoDB.Driver;
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
builder.Services.AddHttpClient("policy-issuance");
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(serviceName))
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter());

var mongoClient = new MongoClient(builder.Configuration.GetConnectionString("MongoDB") ?? "mongodb://localhost:27017");
builder.Services.AddSingleton<IMongoClient>(mongoClient);
builder.Services.AddSingleton<FileProcessingStore>();
builder.Services.AddSingleton<FileBatchKafkaPublisher>();
builder.Services.AddHostedService<FilePollingService>();

var app = builder.Build();
app.UseSerilogRequestLogging();
app.MapHealthChecks("/health");
app.MapControllers();
await app.RunAsync().ConfigureAwait(false);
