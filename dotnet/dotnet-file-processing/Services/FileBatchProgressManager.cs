using dotnet_file_processing.Domain;
using MongoDB.Driver;

namespace dotnet_file_processing.Services;

public sealed class FileBatchProgressManager : IFileBatchProgressManager
{
    private readonly IMongoCollection<BatchRecord> _records;

    public FileBatchProgressManager(IMongoClient mongoClient)
    {
        _records = mongoClient
            .GetDatabase("dotnet_file_processing_db")
            .GetCollection<BatchRecord>("batch_records");
    }

    public async Task<BatchProcessingStatus> GetProgressAsync(string batchId, CancellationToken cancellationToken)
    {
        var succeeded = await _records
            .CountDocumentsAsync(r => r.BatchId == batchId && r.Status == "Succeeded", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var failed = await _records
            .CountDocumentsAsync(r => r.BatchId == batchId && r.Status == "Failed", cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        return new BatchProcessingStatus
        {
            SucceededRecords = (int)succeeded,
            FailedRecords = (int)failed,
            ProcessedRecords = (int)(succeeded + failed)
        };
    }
}
