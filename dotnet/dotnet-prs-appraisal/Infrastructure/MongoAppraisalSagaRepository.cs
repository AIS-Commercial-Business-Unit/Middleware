using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class MongoAppraisalSagaRepository : IAppraisalSagaRepository
{
    private readonly IMongoCollection<AppraisalSagaDocument> _collection;

    public MongoAppraisalSagaRepository(IMongoClient client, string dbName, string collectionName)
    {
        var db = client.GetDatabase(dbName);
        _collection = db.GetCollection<AppraisalSagaDocument>(collectionName);

        var indexKeys = Builders<AppraisalSagaDocument>.IndexKeys.Ascending(d => d.AppraisalId);
        _collection.Indexes.CreateOne(new CreateIndexModel<AppraisalSagaDocument>(
            indexKeys, new CreateIndexOptions { Unique = true }));
    }

    public async Task UpsertAsync(AppraisalSagaRecord record)
    {
        var doc = Map(record);
        var filter = Builders<AppraisalSagaDocument>.Filter.Eq(d => d.AppraisalId, record.AppraisalId);
        await _collection.ReplaceOneAsync(filter, doc, new ReplaceOptions { IsUpsert = true })
            .ConfigureAwait(false);
    }

    public async Task<AppraisalSagaRecord?> GetByAppraisalIdAsync(string appraisalId)
    {
        var filter = Builders<AppraisalSagaDocument>.Filter.Eq(d => d.AppraisalId, appraisalId);
        var doc = await _collection.Find(filter).FirstOrDefaultAsync().ConfigureAwait(false);
        return doc is null ? null : MapBack(doc);
    }

    public async Task<IEnumerable<AppraisalSagaRecord>> GetByStatusAsync(string status)
    {
        var filter = Builders<AppraisalSagaDocument>.Filter.Eq(d => d.Status, status);
        var docs = await _collection.Find(filter).ToListAsync().ConfigureAwait(false);
        return docs.Select(MapBack);
    }

    public async Task<IEnumerable<AppraisalSagaRecord>> GetAllActiveAsync()
    {
        var terminalStatuses = new[] { "Completed", "Failed", "TimedOut" };
        var filter = Builders<AppraisalSagaDocument>.Filter.Nin(d => d.Status, terminalStatuses);
        var docs = await _collection.Find(filter).ToListAsync().ConfigureAwait(false);
        return docs.Select(MapBack);
    }

    private static AppraisalSagaDocument Map(AppraisalSagaRecord r) => new()
    {
        AppraisalId = r.AppraisalId,
        InspectionId = r.InspectionId,
        PolicyNumber = r.PolicyNumber,
        StatusCode = r.StatusCode,
        InspectionTypeCode = r.InspectionTypeCode,
        ProducerCode = r.ProducerCode,
        UWControlCode = r.UWControlCode,
        PLUWCreateComplete = r.PLUWCreateComplete,
        UWDeterminationComplete = r.UWDeterminationComplete,
        PLUWWorkItemId = r.PLUWWorkItemId,
        UWAssignmentType = r.UWAssignmentType,
        SuspenseDays = r.SuspenseDays,
        AssignedTo = r.AssignedTo,
        Status = r.Status,
        FailureReason = r.FailureReason,
        CorrelationId = r.CorrelationId,
        ReceivedAt = r.ReceivedAt,
        CompletedAt = r.CompletedAt,
        UpdatedAt = r.UpdatedAt
    };

    private static AppraisalSagaRecord MapBack(AppraisalSagaDocument d) => new()
    {
        AppraisalId = d.AppraisalId,
        InspectionId = d.InspectionId,
        PolicyNumber = d.PolicyNumber,
        StatusCode = d.StatusCode,
        InspectionTypeCode = d.InspectionTypeCode,
        ProducerCode = d.ProducerCode,
        UWControlCode = d.UWControlCode,
        PLUWCreateComplete = d.PLUWCreateComplete,
        UWDeterminationComplete = d.UWDeterminationComplete,
        PLUWWorkItemId = d.PLUWWorkItemId,
        UWAssignmentType = d.UWAssignmentType,
        SuspenseDays = d.SuspenseDays,
        AssignedTo = d.AssignedTo,
        Status = d.Status,
        FailureReason = d.FailureReason,
        CorrelationId = d.CorrelationId,
        ReceivedAt = d.ReceivedAt,
        CompletedAt = d.CompletedAt,
        UpdatedAt = d.UpdatedAt
    };
}

internal sealed class AppraisalSagaDocument
{
    [BsonId]
    public ObjectId Id { get; set; }
    public string AppraisalId { get; set; } = string.Empty;
    public string InspectionId { get; set; } = string.Empty;
    public string PolicyNumber { get; set; } = string.Empty;
    public int StatusCode { get; set; }
    public string InspectionTypeCode { get; set; } = string.Empty;
    public string ProducerCode { get; set; } = string.Empty;
    public string UWControlCode { get; set; } = string.Empty;
    public bool PLUWCreateComplete { get; set; }
    public bool UWDeterminationComplete { get; set; }
    public string PLUWWorkItemId { get; set; } = string.Empty;
    public string UWAssignmentType { get; set; } = string.Empty;
    public int SuspenseDays { get; set; }
    public string AssignedTo { get; set; } = string.Empty;
    public string Status { get; set; } = "Initiated";
    public string? FailureReason { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    public DateTimeOffset ReceivedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
