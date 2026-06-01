using dotnet_prs_appraisal.Infrastructure;
using dotnet_prs_appraisal.Sagas;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class DocumentRetrievalSagaTests
{
    [Test]
    public async Task WhenAtWorkRequest_SagaPublishesRetrievalRequestedEventAndDoesNotCompleteYet()
    {
        var repository = new FakeDocumentRetrievalRequestRepository();
        var saga = new DocumentRetrievalSaga(repository);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(CreateAtWorkCommand("REQ-DOC-AW-1", "DOC_RiskID_I_TEST001"), context);

        var retrievalEvent = context.PublishedMessages
            .Select(m => m.Message<AppraisalDocumentRetrievalRequestedEvent>())
            .SingleOrDefault(m => m is not null);

        Assert.That(retrievalEvent, Is.Not.Null);
        Assert.That(retrievalEvent!.RequestId, Is.EqualTo("REQ-DOC-AW-1"));
        Assert.That(retrievalEvent.DocumentKey, Is.EqualTo("DOC_RiskID_I_TEST001"));
        Assert.That(saga.Data.AtWorkPending, Is.True);
        Assert.That(repository.CompletedRecords, Is.Empty);
    }

    [Test]
    public async Task WhenAtWorkRetrievedEventArrives_SagaCompletesRepositoryRecord()
    {
        var repository = new FakeDocumentRetrievalRequestRepository();
        var saga = new DocumentRetrievalSaga(repository);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(CreateAtWorkCommand("REQ-DOC-AW-2", "DOC_RiskID_I_TEST001"), context);

        await saga.Handle(new AtWorkDocumentRetrievedEvent
        {
            RequestId = "REQ-DOC-AW-2",
            DocumentKey = "DOC_RiskID_I_TEST001",
            Content = "AAAA",
            MimeType = "application/pdf"
        }, context);

        Assert.That(saga.Data.AtWorkDone, Is.True);
        Assert.That(repository.CompletedRecords, Has.Count.EqualTo(1));
        Assert.That(repository.CompletedRecords[0].RequestId, Is.EqualTo("REQ-DOC-AW-2"));
        Assert.That(repository.CompletedRecords[0].ContentType, Is.EqualTo("application/pdf"));
        Assert.That(repository.CompletedRecords[0].ContentBase64, Is.EqualTo("AAAA"));
        Assert.That(repository.CompletedRecords[0].SourceSystem, Is.EqualTo("AtWork"));
    }

    [Test]
    public async Task WhenAtWorkRequestWithSourceSystemHeader_SagaRoutesToAtWorkPath()
    {
        var repository = new FakeDocumentRetrievalRequestRepository();
        var saga = new DocumentRetrievalSaga(repository);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new RetrieveAppraisalDocumentCommand
        {
            RequestId = "REQ-DOC-AW-3",
            DocumentKey = "ANY-KEY",
            SourceSystem = "AtWork",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(saga.Data.AtWorkPending, Is.True);
        Assert.That(context.PublishedMessages
            .Select(m => m.Message<AppraisalDocumentRetrievalRequestedEvent>())
            .Any(m => m is not null), Is.True);
    }

    [Test]
    public async Task WhenMainframeRequest_SagaSendsAggregationCommandAndDoesNotCompleteYet()
    {
        var repository = new FakeDocumentRetrievalRequestRepository();
        var saga = new DocumentRetrievalSaga(repository);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new RetrieveAppraisalDocumentCommand
        {
            RequestId = "REQ-DOC-MF-1",
            DocumentKey = "MF-DOC-KEY",
            SourceSystem = "Mainframe",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        var sent = context.SentMessages
            .Select(m => m.Message<StartMainframeDocumentAggregationCommand>())
            .SingleOrDefault(m => m is not null);

        Assert.That(sent, Is.Not.Null);
        Assert.That(sent!.RequestId, Is.EqualTo("REQ-DOC-MF-1"));
        Assert.That(repository.CompletedRecords, Is.Empty);
    }

    [Test]
    public async Task WhenMainframeDocumentRetrievedEventArrives_SagaCompletesRepositoryRecord()
    {
        var repository = new FakeDocumentRetrievalRequestRepository();
        var saga = new DocumentRetrievalSaga(repository);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new RetrieveAppraisalDocumentCommand
        {
            RequestId = "REQ-DOC-MF-2",
            DocumentKey = "MF-DOC-KEY",
            SourceSystem = "Mainframe",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Handle(new AppraisalDocumentRetrievedEvent
        {
            RequestId = "REQ-DOC-MF-2",
            DocumentKey = "MF-DOC-KEY",
            SourceSystem = "Mainframe",
            ContentType = "application/pdf",
            ContentBase64 = "BBBB",
            FileName = "mainframe-doc.pdf"
        }, context);

        Assert.That(repository.CompletedRecords, Has.Count.EqualTo(1));
        Assert.That(repository.CompletedRecords[0].RequestId, Is.EqualTo("REQ-DOC-MF-2"));
        Assert.That(repository.CompletedRecords[0].ContentBase64, Is.EqualTo("BBBB"));
        Assert.That(repository.CompletedRecords[0].SourceSystem, Is.EqualTo("Mainframe"));
        Assert.That(repository.CompletedRecords[0].FileName, Is.EqualTo("mainframe-doc.pdf"));
    }

    private static RetrieveAppraisalDocumentCommand CreateAtWorkCommand(string requestId, string documentKey) => new()
    {
        RequestId = requestId,
        DocumentKey = documentKey,
        SourceSystem = "AtWork",
        RequestedAt = DateTimeOffset.UtcNow
    };
}

internal sealed class FakeDocumentRetrievalRequestRepository : IDocumentRetrievalRequestRepository
{
    public List<CompletedDocumentRecord> CompletedRecords { get; } = [];

    public Task CreateAsync(string requestId, string documentKey, string sourceSystem) => Task.CompletedTask;

    public Task<DocumentRetrievalRequestRecord?> FindAsync(string requestId) => Task.FromResult<DocumentRetrievalRequestRecord?>(null);

    public Task CompleteAsync(string requestId, string contentType, string contentBase64, string fileName, string sourceSystem)
    {
        CompletedRecords.Add(new CompletedDocumentRecord(requestId, contentType, contentBase64, fileName, sourceSystem));
        return Task.CompletedTask;
    }
}

internal sealed record CompletedDocumentRecord(
    string RequestId,
    string ContentType,
    string ContentBase64,
    string FileName,
    string SourceSystem);
