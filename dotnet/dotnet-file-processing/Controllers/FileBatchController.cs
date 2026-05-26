using System.Globalization;
using dotnet_file_processing.Domain;
using dotnet_file_processing.Services;
using Microsoft.AspNetCore.Mvc;

namespace dotnet_file_processing.Controllers;

[ApiController]
[Route("api/v1")]
public sealed class FileBatchController : ControllerBase
{
    private static readonly string[][] SampleAccounts =
    [
        ["ACC-ACME-001", "ACME Corporation", "PROD-001"],
        ["ACC-GLOBEX-002", "Globex Industries", "PROD-002"],
        ["ACC-INITECH-003", "Initech LLC", "PROD-003"],
        ["ACC-UMBRELLA-004", "Umbrella Holdings", "PROD-004"],
        ["ACC-WAYNE-005", "Wayne Enterprises", "PROD-005"]
    ];

    private static readonly int[][] PolicyTypes =
    [
        [1, 0], [2, 0], [42, 1], [5, 0], [6, 0], [10, 0]
    ];

    private readonly FileProcessingStore _store;
    private readonly string _inboundDir;

    public FileBatchController(FileProcessingStore store, IConfiguration configuration)
    {
        _store = store;
        _inboundDir = configuration["FileProcessing:InboundDir"] ?? "/app/data/renewals/inbound";
    }

    [HttpGet("batches")]
    public async Task<ActionResult<List<FileBatch>>> GetBatches(CancellationToken cancellationToken)
    {
        return Ok(await _store.GetBatchesAsync(cancellationToken).ConfigureAwait(false));
    }

    [HttpGet("batches/{batchId}")]
    public async Task<ActionResult<FileBatch>> GetBatch(string batchId, CancellationToken cancellationToken)
    {
        var batch = await _store.GetBatchAsync(batchId, cancellationToken).ConfigureAwait(false);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpGet("batches/{batchId}/records")]
    public async Task<ActionResult<List<BatchRecord>>> GetBatchRecords(string batchId, CancellationToken cancellationToken)
    {
        return Ok(await _store.GetRecordsAsync(batchId, cancellationToken).ConfigureAwait(false));
    }

    [HttpPost("batches/generate")]
    public async Task<IActionResult> GenerateSampleBatch([FromQuery] int count = 10, CancellationToken cancellationToken = default)
    {
        count = Math.Clamp(count, 1, 50);
        Directory.CreateDirectory(_inboundDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        var fileName = $"RENEWAL_{timestamp}.csv";
        var path = Path.Combine(_inboundDir, fileName);
        var lines = new List<string>
        {
            "PolicyNumber,ExpirationDate,InsuredName,PolicyTypeCode,PolicyTypeSubCode,PremiumAmount,ProducerCode,BillingType,AccountId"
        };

        for (var index = 0; index < count; index++)
        {
            var account = SampleAccounts[index % SampleAccounts.Length];
            var policy = PolicyTypes[index % PolicyTypes.Length];
            var policyNumber = $"POL-{index + 1:000}-{DateTime.UtcNow.Year}";
            var expirationDate = DateTime.UtcNow.Date.AddDays(60 + index).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            var premium = (1000 + index * 250).ToString("F2", CultureInfo.InvariantCulture);
            lines.Add(string.Join(',', policyNumber, expirationDate, account[1], policy[0], policy[1], premium, account[2], "DirectBill", account[0]));
        }

        await System.IO.File.WriteAllLinesAsync(path, lines, cancellationToken).ConfigureAwait(false);
        return StatusCode(201, new { fileName, recordCount = count, message = "File created in inbound folder" });
    }
}
