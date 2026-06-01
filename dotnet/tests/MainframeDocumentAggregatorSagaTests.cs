using System.Text;
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
    public async Task WhenFinalChunkArrives_SagaPublishesAssembledBase64Document()
    {
        var adapter = new FakeArtemisAdapter();
        var saga = new MainframeDocumentAggregatorSaga(adapter, NullLogger<MainframeDocumentAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand();

        await saga.Handle(command, context);
        await saga.Handle(CreateChunk(command.RequestId, "chunk-1", isFinal: false), context);
        await saga.Handle(CreateChunk(command.RequestId, "chunk-2", isFinal: false), context);
        await saga.Handle(CreateChunk(command.RequestId, "chunk-3", isFinal: true), context);

        Assert.That(adapter.DocumentRequests, Is.EqualTo(1));
        var completed = context.PublishedMessages
            .Select(message => message.Message<AppraisalDocumentRetrievedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.DocumentKey, Is.EqualTo(command.DocumentKey));
        Assert.That(completed.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(completed.ContentBase64, Is.EqualTo(Convert.ToBase64String(Encoding.UTF8.GetBytes("chunk-1chunk-2chunk-3"))));
    }

    [Test]
    public async Task WhenTimeoutOccursWithoutFinalChunk_SagaPublishesEmptyCompletion()
    {
        var saga = new MainframeDocumentAggregatorSaga(new FakeArtemisAdapter(), NullLogger<MainframeDocumentAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand();

        await saga.Handle(command, context);
        await saga.Handle(CreateChunk(command.RequestId, "chunk-1", isFinal: false), context);
        await saga.Timeout(new MainframeDocumentAggregatorTimeoutMessage { RequestId = command.RequestId }, context);

        var completed = context.PublishedMessages
            .Select(message => message.Message<AppraisalDocumentRetrievedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.DocumentKey, Is.EqualTo(command.DocumentKey));
        Assert.That(completed.ContentBase64, Is.EqualTo(string.Empty));
        Assert.That(completed.ContentType, Is.EqualTo(string.Empty));
        Assert.That(completed.FileName, Is.EqualTo(string.Empty));
    }

    private static StartMainframeDocumentAggregationCommand CreateStartCommand() => new()
    {
        RequestId = "REQ-DOC-1",
        DocumentKey = "MF-DOC-1",
        RequestedAt = DateTimeOffset.UtcNow
    };

    private static MainframeDocumentChunkReceivedEvent CreateChunk(string requestId, string payload, bool isFinal) => new()
    {
        RequestId = requestId,
        ChunkPayload = payload,
        IsFinal = isFinal
    };
}
