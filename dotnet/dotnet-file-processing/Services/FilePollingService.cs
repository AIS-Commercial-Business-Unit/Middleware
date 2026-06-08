using Middleware.Contracts.Events;
using NServiceBus;

namespace dotnet_file_processing.Services;

public sealed class FilePollingService : BackgroundService
{
    private readonly IMessageSession _messageSession;
    private readonly string _inboundDir;
    private readonly string _processingDir;
    private readonly ILogger<FilePollingService> _logger;

    public FilePollingService(
        IMessageSession messageSession,
        IConfiguration configuration,
        ILogger<FilePollingService> logger)
    {
        _messageSession = messageSession;
        _inboundDir = configuration["FileProcessing:InboundDir"] ?? "/app/data/renewals/inbound";
        _processingDir = configuration["FileProcessing:ProcessingDir"] ?? "/app/data/renewals/processing";
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_inboundDir);
        Directory.CreateDirectory(_processingDir);

        _logger.LogInformation(
            "File polling started — inboundDir={InboundDir} processingDir={ProcessingDir}",
            _inboundDir,
            _processingDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            var files = Directory.EnumerateFiles(_inboundDir, "*.*")
                .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (files.Count > 0)
            {
                _logger.LogInformation(
                    "File polling detected files — fileCount={FileCount} inboundDir={InboundDir}",
                    files.Count,
                    _inboundDir);
            }

            foreach (var file in files)
            {
                try
                {
                    await EnqueueFileAsync(file, stoppingToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Failed to enqueue file — filePath={FilePath}", file);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task EnqueueFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var fileSize = fileInfo.Length; // read before move — FileInfo.Length queries live filesystem
        var batchId = Guid.NewGuid().ToString();

        // Move immediately to prevent re-detection on the next polling cycle.
        var processingPath = Path.Combine(_processingDir, fileInfo.Name);
        if (File.Exists(processingPath))
            File.Delete(processingPath);
        File.Move(filePath, processingPath);

        _logger.LogInformation(
            "File detected and queued for NServiceBus processing — batchId={BatchId} fileName={FileName} fileSizeBytes={FileSizeBytes}",
            batchId,
            fileInfo.Name,
            fileSize);

        await _messageSession.Publish(new FileRetrievedDetectedEvent
        {
            BatchId = batchId,
            FileName = fileInfo.Name,
            FilePath = processingPath,
            FileSizeBytes = fileSize,
            DetectedAt = DateTimeOffset.UtcNow
        }, cancellationToken: cancellationToken).ConfigureAwait(false);
    }
}
