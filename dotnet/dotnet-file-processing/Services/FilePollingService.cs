using System.Net.Http.Json;
using System.Text.Json;
using dotnet_file_processing.Domain;
using Serilog;

namespace dotnet_file_processing.Services;

public sealed class FilePollingService : BackgroundService
{
    private readonly FileProcessingStore _store;
    private readonly FileBatchKafkaPublisher _publisher;
    private readonly HttpClient _httpClient;
    private readonly string _inboundDir;
    private readonly string _processedDir;
    private readonly string _errorDir;
    private readonly string _policyIssuanceUrl;

    public FilePollingService(
        FileProcessingStore store,
        FileBatchKafkaPublisher publisher,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _store = store;
        _publisher = publisher;
        _httpClient = httpClientFactory.CreateClient("policy-issuance");
        _inboundDir = configuration["FileProcessing:InboundDir"] ?? "/app/data/renewals/inbound";
        _processedDir = configuration["FileProcessing:ProcessedDir"] ?? "/app/data/renewals/processed";
        _errorDir = configuration["FileProcessing:ErrorDir"] ?? "/app/data/renewals/error";
        _policyIssuanceUrl = configuration["ExternalServices:PolicyIssuanceUrl"] ?? "http://dotnet-policy-issuance:8181";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Directory.CreateDirectory(_inboundDir);
        Directory.CreateDirectory(_processedDir);
        Directory.CreateDirectory(_errorDir);

        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var file in Directory.EnumerateFiles(_inboundDir, "*.*")
                         .Where(path => path.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                await ProcessFileAsync(file, stoppingToken).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task ProcessFileAsync(string filePath, CancellationToken cancellationToken)
    {
        var fileInfo = new FileInfo(filePath);
        var batch = new FileBatch
        {
            BatchId = Guid.NewGuid().ToString(),
            FileName = fileInfo.Name,
            FileLocationReference = filePath,
            FileSizeBytes = fileInfo.Length,
            Status = "Parsing",
            ReceivedAt = DateTimeOffset.UtcNow
        };

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath, cancellationToken).ConfigureAwait(false);
            var dataLines = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();
            batch.TotalRecords = dataLines.Count;
            batch.PercentComplete = 0;
            await _store.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            await _publisher.PublishAsync("file.events.file-batch-started", new
            {
                batchId = batch.BatchId,
                fileName = batch.FileName,
                recordCount = batch.TotalRecords,
                startedAt = batch.ReceivedAt
            }, cancellationToken).ConfigureAwait(false);

            var sequence = 0;
            foreach (var line in dataLines)
            {
                sequence++;
                var renewal = ParseLine(line);
                var record = new BatchRecord
                {
                    RecordId = Guid.NewGuid().ToString(),
                    BatchId = batch.BatchId,
                    SequenceNumber = sequence,
                    RawContent = line,
                    Status = "Processing"
                };

                await _store.UpsertRecordAsync(record, cancellationToken).ConfigureAwait(false);

                var issuanceId = Guid.NewGuid().ToString();
                var response = await _httpClient.PostAsJsonAsync(
                        $"{_policyIssuanceUrl}/api/v1/policies/issue",
                        new
                        {
                            issuanceId,
                            accountId = renewal.AccountId,
                            submittingChannel = "AutomatedRenewal",
                            requestedAt = DateTimeOffset.UtcNow,
                            batchId = batch.BatchId,
                            recordId = record.RecordId,
                            policies = new[]
                            {
                                new
                                {
                                    policyTypeCode = renewal.PolicyTypeCode,
                                    policyTypeSubCode = renewal.PolicyTypeSubCode,
                                    policyData = new { renewal.PolicyNumber, renewal.ExpirationDate, renewal.PremiumAmount }
                                }
                            }
                        }, cancellationToken)
                    .ConfigureAwait(false);

                if (response.IsSuccessStatusCode)
                {
                    record.Status = "Succeeded";
                    record.CorrelationId = issuanceId;
                    record.ProcessorResult = JsonSerializer.Serialize(new { policyNumbers = new[] { issuanceId }, issuanceId });
                    batch.SucceededRecords++;
                }
                else
                {
                    record.Status = "Failed";
                    record.ProcessorResult = JsonSerializer.Serialize(new { error = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false) });
                    batch.FailedRecords++;
                }

                record.ProcessedAt = DateTimeOffset.UtcNow;
                batch.ProcessedRecords++;
                batch.PercentComplete = batch.TotalRecords == 0 ? 100 : (double)batch.ProcessedRecords / batch.TotalRecords.Value * 100;
                batch.Status = batch.ProcessedRecords == batch.TotalRecords ? (batch.FailedRecords == 0 ? "Completed" : "PartialFailure") : "Processing";

                await _store.UpsertRecordAsync(record, cancellationToken).ConfigureAwait(false);
                await _store.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);
            }

            batch.ParsingCompletedAt = batch.ParsingCompletedAt ?? DateTimeOffset.UtcNow;
            batch.ProcessingCompletedAt = DateTimeOffset.UtcNow;
            batch.Status = batch.FailedRecords == 0 ? "Completed" : "PartialFailure";
            batch.PercentComplete = 100;
            await _store.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);

            var destination = Path.Combine(batch.FailedRecords == 0 ? _processedDir : _errorDir, batch.FileName);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            File.Move(filePath, destination);

            await _publisher.PublishAsync("file.events.file-batch-completed", new
            {
                batchId = batch.BatchId,
                fileName = batch.FileName,
                recordCount = batch.TotalRecords,
                succeededRecords = batch.SucceededRecords,
                failedRecords = batch.FailedRecords,
                completedAt = batch.ProcessingCompletedAt
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to process renewal file {FileName}", batch.FileName);
            batch.Status = "Failed";
            batch.ProcessingCompletedAt = DateTimeOffset.UtcNow;
            batch.PercentComplete = 100;
            await _store.UpsertBatchAsync(batch, cancellationToken).ConfigureAwait(false);

            var destination = Path.Combine(_errorDir, batch.FileName);
            if (File.Exists(destination))
            {
                File.Delete(destination);
            }

            if (File.Exists(filePath))
            {
                File.Move(filePath, destination);
            }
        }
    }

    private static RenewalRecord ParseLine(string line)
    {
        var parts = line.Split(',');
        return new RenewalRecord(
            PolicyNumber: parts.ElementAtOrDefault(0) ?? string.Empty,
            ExpirationDate: parts.ElementAtOrDefault(1) ?? string.Empty,
            InsuredName: parts.ElementAtOrDefault(2) ?? string.Empty,
            PolicyTypeCode: int.TryParse(parts.ElementAtOrDefault(3), out var typeCode) ? typeCode : 1,
            PolicyTypeSubCode: int.TryParse(parts.ElementAtOrDefault(4), out var subCode) ? subCode : 0,
            PremiumAmount: decimal.TryParse(parts.ElementAtOrDefault(5), out var premium) ? premium : 0,
            ProducerCode: parts.ElementAtOrDefault(6) ?? string.Empty,
            BillingType: parts.ElementAtOrDefault(7) ?? "DirectBill",
            AccountId: parts.ElementAtOrDefault(8) ?? $"ACC-{Guid.NewGuid():N}"[..12].ToUpperInvariant());
    }

    private sealed record RenewalRecord(
        string PolicyNumber,
        string ExpirationDate,
        string InsuredName,
        int PolicyTypeCode,
        int PolicyTypeSubCode,
        decimal PremiumAmount,
        string ProducerCode,
        string BillingType,
        string AccountId);
}
