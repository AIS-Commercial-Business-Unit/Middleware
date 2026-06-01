using Middleware.Contracts.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class MongoDocumentListRequestRepository : IDocumentListRequestRepository
{
    private readonly IMongoCollection<DocumentListDocument> _collection;

    public MongoDocumentListRequestRepository(IMongoClient client, string dbName)
    {
        var db = client.GetDatabase(dbName);
        _collection = db.GetCollection<DocumentListDocument>("document_list_requests");

        _collection.Indexes.CreateOne(new CreateIndexModel<DocumentListDocument>(
            Builders<DocumentListDocument>.IndexKeys.Ascending(d => d.RequestId),
            new CreateIndexOptions { Unique = true }));

        // Auto-expire records after 24 hours to prevent unbounded growth.
        _collection.Indexes.CreateOne(new CreateIndexModel<DocumentListDocument>(
            Builders<DocumentListDocument>.IndexKeys.Ascending(d => d.CreatedAt),
            new CreateIndexOptions { ExpireAfter = TimeSpan.FromHours(24) }));
    }

    public async Task CreateAsync(string requestId, string policyNumber)
    {
        var doc = new DocumentListDocument
        {
            RequestId = requestId,
            PolicyNumber = policyNumber,
            Status = "Pending",
            Documents = new List<DocumentSummaryBson>(),
            PartialResult = false,
            CreatedAt = DateTime.UtcNow
        };
        await _collection.InsertOneAsync(doc).ConfigureAwait(false);
    }

    public async Task<DocumentListRequestRecord?> FindAsync(string requestId)
    {
        var filter = Builders<DocumentListDocument>.Filter.Eq(d => d.RequestId, requestId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
        return doc is null ? null : MapBack(doc);
    }

    public async Task CompleteAsync(string requestId, List<AppraisalDocumentSummary> documents, bool partialResult)
    {
        var filter = Builders<DocumentListDocument>.Filter.Eq(d => d.RequestId, requestId);
        var update = Builders<DocumentListDocument>.Update
            .Set(d => d.Status, partialResult ? "TimedOut" : "Complete")
            .Set(d => d.Documents, documents.Select(ToDoc).ToList())
            .Set(d => d.PartialResult, partialResult)
            .Set(d => d.CompletedAt, DateTime.UtcNow);
        await _collection.UpdateOneAsync(filter, update).ConfigureAwait(false);
    }

    private static DocumentListRequestRecord MapBack(DocumentListDocument d) => new()
    {
        RequestId = d.RequestId,
        PolicyNumber = d.PolicyNumber,
        Status = d.Status,
        Documents = d.Documents.Select(FromDoc).ToList(),
        PartialResult = d.PartialResult,
        CreatedAt = d.CreatedAt,
        CompletedAt = d.CompletedAt
    };

    private static DocumentSummaryBson ToDoc(AppraisalDocumentSummary s) => new()
    {
        DocumentId = s.DocumentId,
        DocumentKey = s.DocumentKey,
        SourceSystem = s.SourceSystem,
        DocumentType = s.DocumentType,
        DocumentName = s.DocumentName,
        DocumentDate = s.DocumentDate,
        PolicyNumber = s.PolicyNumber,
        Status = s.Status
    };

    private static AppraisalDocumentSummary FromDoc(DocumentSummaryBson b) => new()
    {
        DocumentId = b.DocumentId,
        DocumentKey = b.DocumentKey,
        SourceSystem = b.SourceSystem,
        DocumentType = b.DocumentType,
        DocumentName = b.DocumentName,
        DocumentDate = b.DocumentDate,
        PolicyNumber = b.PolicyNumber,
        Status = b.Status
    };
}

internal sealed class DocumentListDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string RequestId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public List<DocumentSummaryBson> Documents { get; set; } = new();
    public bool PartialResult { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

internal sealed class DocumentSummaryBson
{
    public string DocumentId { get; set; } = string.Empty;
    public string DocumentKey { get; set; } = string.Empty;
    public string SourceSystem { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentDate { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}
