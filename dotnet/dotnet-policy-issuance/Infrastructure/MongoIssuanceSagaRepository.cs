using dotnet_policy_issuance.Domain;
using MongoDB.Driver;

namespace dotnet_policy_issuance.Infrastructure;

public sealed class MongoIssuanceSagaRepository : IIssuanceSagaRecordRepository
{
    private readonly IMongoCollection<IssuanceSagaRecord> _collection;

    public MongoIssuanceSagaRepository(IMongoClient client, string databaseName, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(client);
        _collection = client.GetDatabase(databaseName).GetCollection<IssuanceSagaRecord>(collectionName);
    }

    public async Task<IssuanceSagaRecord?> GetAsync(string issuanceId, CancellationToken cancellationToken = default)
    {
        return await _collection.Find(record => record.IssuanceId == issuanceId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task UpsertAsync(IssuanceSagaRecord record, CancellationToken cancellationToken = default)
    {
        await _collection.ReplaceOneAsync(
                existing => existing.IssuanceId == record.IssuanceId,
                record,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken)
            .ConfigureAwait(false);
    }
}
