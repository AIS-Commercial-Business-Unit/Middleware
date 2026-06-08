using dotnet_prs_appraisal.Infrastructure;
using dotnet_prs_appraisal.Sagas;
using Microsoft.Extensions.Logging.Abstractions;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class MainframeListAggregatorSagaTests
{
    [Test]
    public async Task WhenStarted_SagaSendsMainframeListRequest()
    {
        var adapter = new FakeArtemisAdapter();
        var accumulatorRepository = new FakeAccumulatorRepository();
        var saga = new MainframeListAggregatorSaga(accumulatorRepository, adapter, NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new AppraisalDocumentListRequestedEvent
        {
            RequestId = "REQ-LIST-1",
            PolicyNumber = "POL123",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(adapter.ListRequestCount, Is.EqualTo(1));
        Assert.That(context.PublishedMessages, Is.Empty);
    }

    [Test]
    public async Task WhenAccumulationCompletes_SagaPublishesCompletedList()
    {
        var adapter = new FakeArtemisAdapter();
        var accumulatorRepository = new FakeAccumulatorRepository();
        var saga = new MainframeListAggregatorSaga(accumulatorRepository, adapter, NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new AppraisalDocumentListRequestedEvent
        {
            RequestId = "REQ-LIST-2",
            PolicyNumber = "POL456",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Handle(new MainframeListAccumulationCompleteEvent
        {
            RequestId = "REQ-LIST-2",
            Documents =
            [
                CreateDocument("DOC-1"),
                CreateDocument("DOC-2")
            ]
        }, context);

        var published = context.PublishedMessages
            .Select(entry => entry.Message<MainframeDocumentListCompletedEvent>())
            .SingleOrDefault(message => message is not null);

        Assert.That(published, Is.Not.Null);
        Assert.That(published!.RequestId, Is.EqualTo("REQ-LIST-2"));
        Assert.That(published.Documents.Select(document => document.DocumentKey), Is.EqualTo(new[] { "DOC-1", "DOC-2" }));
    }

    [Test]
    public async Task WhenTimeoutFires_SagaPublishesPartialListFromAccumulatorStore()
    {
        var adapter = new FakeArtemisAdapter();
        var accumulatorRepository = new FakeAccumulatorRepository
        {
            ListDocuments =
            [
                CreateDocument("DOC-A"),
                CreateDocument("DOC-B")
            ]
        };
        var saga = new MainframeListAggregatorSaga(accumulatorRepository, adapter, NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new AppraisalDocumentListRequestedEvent
        {
            RequestId = "REQ-LIST-3",
            PolicyNumber = "POL789",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Timeout(new MainframeListAggregatorTimeoutMessage
        {
            RequestId = "REQ-LIST-3"
        }, context);

        var published = context.PublishedMessages
            .Select(entry => entry.Message<MainframeDocumentListCompletedEvent>())
            .SingleOrDefault(message => message is not null);

        Assert.That(published, Is.Not.Null);
        Assert.That(published!.Documents.Select(document => document.DocumentKey), Is.EqualTo(new[] { "DOC-A", "DOC-B" }));
    }

    private static AppraisalDocumentSummary CreateDocument(string documentKey) => new()
    {
        DocumentKey = documentKey,
        SourceSystem = "Mainframe"
    };
}

internal sealed class FakeAccumulatorRepository : IAccumulatorRepository
{
    public List<AppraisalDocumentSummary> ListDocuments { get; set; } = [];

    public Task EnsureCreatedAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateListPartAsync(string requestId, int sequenceNumber, int totalExpected, AppraisalDocumentSummary document, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<(bool won, List<AppraisalDocumentSummary> documents)> TryCompleteListAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult((false, ListDocuments));

    public Task<List<AppraisalDocumentSummary>> GetListDocumentsAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult(ListDocuments);

    public Task CreateDocumentChunkAsync(string requestId, string chunkPayload, bool isFinal, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    public Task<(bool won, string assembledContent)> TryCompleteDocumentAsync(string requestId, CancellationToken cancellationToken = default)
        => Task.FromResult((false, string.Empty));
}

internal sealed class FakeArtemisAdapter : IArtemisAdapter
{
    public int ListRequestCount { get; private set; }

    public int DocumentRequestCount { get; private set; }

    public void SendListRequest(string requestId, string policyNumber)
        => ListRequestCount++;

    public void SendDocumentRequest(string requestId, string documentKey)
        => DocumentRequestCount++;
}
