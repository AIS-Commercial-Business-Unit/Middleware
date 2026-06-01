using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class MongoDocumentRetrievalRequestRepository : IDocumentRetrievalRequestRepository
{
    private readonly IMongoCollection<DocumentRetrievalDocument> _collection;

    public MongoDocumentRetrievalRequestRepository(IMongoClient client, string dbName)
    {
        var db = client.GetDatabase(dbName);
        _collection = db.GetCollection<DocumentRetrievalDocument>("document_retrieval_requests");

        _collection.Indexes.CreateOne(new CreateIndexModel<DocumentRetrievalDocument>(
            Builders<DocumentRetrievalDocument>.IndexKeys.Ascending(d => d.RequestId),
            new CreateIndexOptions { Unique = true }));

        // Auto-expire records after 24 hours to prevent unbounded growth.
        _collection.Indexes.CreateOne(new CreateIndexModel<DocumentRetrievalDocument>(
            Builders<DocumentRetrievalDocument>.IndexKeys.Ascending(d => d.CreatedAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }));
    }

    public async Task CreateAsync(string requestId, string documentKey, string sourceSystem)
    {
        var doc = new DocumentRetrievalDocument
        {
            RequestId = requestId,
            DocumentKey = documentKey,
            SourceSystem = sourceSystem,
            Status = "Pending",
            CreatedAt = DateTime.UtcNow
        };
        await _collection.InsertOneAsync(doc).ConfigureAwait(false);
    }

    public async Task<DocumentRetrievalRequestRecord?> FindAsync(string requestId)
    {
        var filter = Builders<DocumentRetrievalDocument>.Filter.Eq(d => d.RequestId, requestId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
        return doc is null ? null : MapBack(doc);
    }

    public async Task CompleteAsync(string requestId, string contentType, string contentBase64, string fileName, string sourceSystem)
    {
        var filter = Builders<DocumentRetrievalDocument>.Filter.Eq(d => d.RequestId, requestId);
        var update = Builders<DocumentRetrievalDocument>.Update
            .Set(d => d.Status, "Complete")
            .Set(d => d.ContentType, contentType)
            .Set(d => d.ContentBase64, contentBase64)
            .Set(d => d.FileName, fileName)
            .Set(d => d.SourceSystem, sourceSystem)
            .Set(d => d.CompletedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
    }

    private static DocumentRetrievalRequestRecord MapBack(DocumentRetrievalDocument d) => new()
    {
        RequestId = d.RequestId,
        DocumentKey = d.DocumentKey,
        SourceSystem = d.SourceSystem,
        Status = d.Status,
        ContentType = d.ContentType,
        ContentBase64 = d.ContentBase64,
        FileName = d.FileName,
        CreatedAt = d.CreatedAt,
        CompletedAt = d.CompletedAt
    };
}

internal sealed class DocumentRetrievalDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string DocumentKey { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string ContentType { get; set; } = string.Empty;
    public string ContentBase64 { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}
