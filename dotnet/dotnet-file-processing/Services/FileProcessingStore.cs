using dotnet_file_processing.Domain;
using MongoDB.Driver;

namespace dotnet_file_processing.Services;

public sealed class FileProcessingStore
{
    private readonly IMongoCollection<FileBatch> _batches;
    private readonly IMongoCollection<BatchRecord> _records;

    public FileProcessingStore(IMongoClient client)
    {
        var database = client.GetDatabase("middleware-platform");
        _batches = database.GetCollection<FileBatch>("file_batches");
        _records = database.GetCollection<BatchRecord>("batch_records");
    }

    public async Task<List<FileBatch>> GetBatchesAsync(CancellationToken cancellationToken)
    {
        return await _batches.Find(FilterDefinition<FileBatch>.Empty)
            .SortByDescending(batch => batch.ReceivedAt)
            .Limit(100)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<FileBatch?> GetBatchAsync(string batchId, CancellationToken cancellationToken)
    {
        return await _batches.Find(batch => batch.BatchId == batchId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<BatchRecord>> GetRecordsAsync(string batchId, CancellationToken cancellationToken)
    {
        return await _records.Find(record => record.BatchId == batchId)
            .SortBy(record => record.SequenceNumber)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertBatchAsync(FileBatch batch, CancellationToken cancellationToken)
    {
        await _batches.ReplaceOneAsync(
                existing => existing.BatchId == batch.BatchId,
                batch,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertRecordAsync(BatchRecord record, CancellationToken cancellationToken)
    {
        await _records.ReplaceOneAsync(
                existing => existing.RecordId == record.RecordId,
                record,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
