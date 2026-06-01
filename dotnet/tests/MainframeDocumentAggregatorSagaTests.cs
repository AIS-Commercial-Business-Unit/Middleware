using dotnet_prs_appraisal.Sagas;
using Microsoft.Extensions.Logging.Abstractions;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class MainframeDocumentAggregatorSagaTests
{
    [Test]
    public async Task WhenStarted_SagaRequestsDocumentFromMainframe()
    {
        var adapter = new FakeArtemisAdapter();
        var saga = new MainframeDocumentAggregatorSaga(adapter, NullLogger<MainframeDocumentAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new StartMainframeDocumentAggregationCommand
        {
            RequestId = "REQ-DOC-1",
            DocumentKey = "DOC-123",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        Assert.That(adapter.DocumentRequestCount, Is.EqualTo(1));
        Assert.That(context.PublishedMessages, Is.Empty);
    }

    [Test]
    public async Task WhenAccumulationCompletes_SagaPublishesRetrievedDocument()
    {
        var adapter = new FakeArtemisAdapter();
        var saga = new MainframeDocumentAggregatorSaga(adapter, NullLogger<MainframeDocumentAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new StartMainframeDocumentAggregationCommand
        {
            RequestId = "REQ-DOC-2",
            DocumentKey = "DOC-456",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Handle(new MainframeDocumentAccumulationCompleteEvent
        {
            RequestId = "REQ-DOC-2",
            DocumentKey = "DOC-456",
            ContentBase64 = "BBBB"
        }, context);

        var published = context.PublishedMessages
            .Select(entry => entry.Message<AppraisalDocumentRetrievedEvent>())
            .SingleOrDefault(message => message is not null);

        Assert.That(published, Is.Not.Null);
        Assert.That(published!.RequestId, Is.EqualTo("REQ-DOC-2"));
        Assert.That(published.DocumentKey, Is.EqualTo("DOC-456"));
        Assert.That(published.SourceSystem, Is.EqualTo("Mainframe"));
        Assert.That(published.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(published.ContentBase64, Is.EqualTo("BBBB"));
        Assert.That(published.FileName, Is.EqualTo("appraisal-DOC-456.pdf"));
    }

    [Test]
    public async Task WhenTimeoutFires_SagaPublishesEmptyDocument()
    {
        var adapter = new FakeArtemisAdapter();
        var saga = new MainframeDocumentAggregatorSaga(adapter, NullLogger<MainframeDocumentAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new StartMainframeDocumentAggregationCommand
        {
            RequestId = "REQ-DOC-3",
            DocumentKey = "DOC-EMPTY",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Timeout(new MainframeDocumentAggregatorTimeoutMessage
        {
            RequestId = "REQ-DOC-3"
        }, context);

        var published = context.PublishedMessages
            .Select(entry => entry.Message<AppraisalDocumentRetrievedEvent>())
            .SingleOrDefault(message => message is not null);

        Assert.That(published, Is.Not.Null);
        Assert.That(published!.RequestId, Is.EqualTo("REQ-DOC-3"));
        Assert.That(published.DocumentKey, Is.EqualTo("DOC-EMPTY"));
        Assert.That(published.ContentBase64, Is.EqualTo(string.Empty));
        Assert.That(published.ContentType, Is.EqualTo(string.Empty));
    }
}
