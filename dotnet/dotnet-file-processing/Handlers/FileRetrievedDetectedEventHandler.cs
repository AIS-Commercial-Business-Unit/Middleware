using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;
using Serilog.Context;

namespace dotnet_file_processing.Handlers;

public sealed class FileRetrievedDetectedEventHandler : IHandleMessages<FileRetrievedDetectedEvent>
{
    private readonly ILogger<FileRetrievedDetectedEventHandler> _logger;

    public FileRetrievedDetectedEventHandler(ILogger<FileRetrievedDetectedEventHandler> logger)
    {
        _logger = logger;
    }

    public async Task Handle(FileRetrievedDetectedEvent message, IMessageHandlerContext context)
    {
        using var _ = LogContext.PushProperty("BatchId", message.BatchId);

        _logger.LogInformation(
            "File retrieved event received — batchId={BatchId} fileName={FileName} filePath={FilePath}",
            message.BatchId,
            message.FileName,
            message.FilePath);

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(message.FilePath, context.CancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to read file — batchId={BatchId} filePath={FilePath}",
                message.BatchId, message.FilePath);
            throw;
        }

        var dataLines = lines.Skip(1).Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

        _logger.LogInformation(
            "File parsed — batchId={BatchId} fileName={FileName} recordCount={RecordCount}",
            message.BatchId,
            message.FileName,
            dataLines.Count);

        await context.Publish(new FileBatchStartedEvent
        {
            BatchId = message.BatchId,
            FileName = message.FileName,
            RecordCount = dataLines.Count,
            StartedAt = message.DetectedAt
        }).ConfigureAwait(false);

        var sequence = 0;
        foreach (var line in dataLines)
        {
            sequence++;
            var renewal = ParseLine(line);

            await context.Send(new IssuePolicyCommand
            {
                IssuanceId = Guid.NewGuid().ToString(),
                AccountId = renewal.AccountId,
                SubmittingChannel = "AutomatedRenewal",
                RequestedAt = DateTimeOffset.UtcNow,
                BatchId = message.BatchId,
                RecordId = Guid.NewGuid().ToString(),
                Policies =
                [
                    new PolicyItem
                    {
                        PolicyTypeCode = renewal.PolicyTypeCode,
                        PolicyTypeSubCode = renewal.PolicyTypeSubCode,
                        PolicyNumber = renewal.PolicyNumber,
                        ExpirationDate = renewal.ExpirationDate,
                        PremiumAmount = renewal.PremiumAmount,
                        InsuredName = renewal.InsuredName
                    }
                ]
            }).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "File batch dispatched to issuance — batchId={BatchId} fileName={FileName} dispatchedCount={DispatchedCount}",
            message.BatchId,
            message.FileName,
            dataLines.Count);
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
            PremiumAmount: decimal.TryParse(parts.ElementAtOrDefault(5), out var premium) ? premium : 0m,
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