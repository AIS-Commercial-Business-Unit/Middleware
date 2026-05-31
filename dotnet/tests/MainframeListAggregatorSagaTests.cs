using dotnet_prs_appraisal.Infrastructure;
using dotnet_prs_appraisal.Sagas;
using Microsoft.Extensions.Logging.Abstractions;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class MainframeListAggregatorSagaTests
{
    [Test]
    public async Task WhenAllPartsReceived_SagaPublishesCompletedEventWithAllDocuments()
    {
        var adapter = new FakeArtemisAdapter();
        var saga = new MainframeListAggregatorSaga(adapter, NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand();

        await saga.Handle(command, context);
        await saga.Handle(CreatePart(command.RequestId, 1, 3, "DOC-1"), context);
        await saga.Handle(CreatePart(command.RequestId, 2, 3, "DOC-2"), context);
        await saga.Handle(CreatePart(command.RequestId, 3, 3, "DOC-3"), context);

        Assert.That(adapter.ListRequests, Is.EqualTo(1));
        var completed = context.PublishedMessages
            .Select(message => message.Message<Uc4MainframeDocumentListCompletedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Documents.Select(document => document.DocumentKey), Is.EqualTo(new[] { "DOC-1", "DOC-2", "DOC-3" }));
    }

    [Test]
    public async Task WhenTimeoutOccursWithPartialParts_SagaPublishesPartialCompletion()
    {
        var saga = new MainframeListAggregatorSaga(new FakeArtemisAdapter(), NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand();

        await saga.Handle(command, context);
        await saga.Handle(CreatePart(command.RequestId, 1, 3, "DOC-1"), context);
        await saga.Handle(CreatePart(command.RequestId, 2, 3, "DOC-2"), context);
        await saga.Timeout(new Uc4MainframeListAggregatorTimeoutMessage { RequestId = command.RequestId }, context);

        var completed = context.PublishedMessages
            .Select(message => message.Message<Uc4MainframeDocumentListCompletedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Documents.Select(document => document.DocumentKey), Is.EqualTo(new[] { "DOC-1", "DOC-2" }));
    }

    [Test]
    public async Task WhenPartsArriveOutOfOrder_SagaPublishesDocumentsInSequenceOrder()
    {
        var saga = new MainframeListAggregatorSaga(new FakeArtemisAdapter(), NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand();

        await saga.Handle(command, context);
        await saga.Handle(CreatePart(command.RequestId, 3, 3, "DOC-3"), context);
        await saga.Handle(CreatePart(command.RequestId, 1, 3, "DOC-1"), context);
        await saga.Handle(CreatePart(command.RequestId, 2, 3, "DOC-2"), context);

        var completed = context.PublishedMessages
            .Select(message => message.Message<Uc4MainframeDocumentListCompletedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Documents.Select(document => document.DocumentKey), Is.EqualTo(new[] { "DOC-1", "DOC-2", "DOC-3" }));
    }

    [Test]
    public async Task WhenDuplicateSequenceArrives_SagaOverwritesDocumentWithoutDoubleCounting()
    {
        var saga = new MainframeListAggregatorSaga(new FakeArtemisAdapter(), NullLogger<MainframeListAggregatorSaga>.Instance);
        var context = new TestableMessageHandlerContext();
        var command = CreateStartCommand(totalExpected: 2);

        await saga.Handle(command, context);
        await saga.Handle(CreatePart(command.RequestId, 1, 2, "DOC-OLD", "Old name"), context);
        await saga.Handle(CreatePart(command.RequestId, 1, 2, "DOC-NEW", "Updated name"), context);
        await saga.Handle(CreatePart(command.RequestId, 2, 2, "DOC-2"), context);

        var completed = context.PublishedMessages
            .Select(message => message.Message<Uc4MainframeDocumentListCompletedEvent>())
            .LastOrDefault(message => message is not null);

        Assert.That(completed, Is.Not.Null);
        Assert.That(completed!.Documents.Count, Is.EqualTo(2));
        Assert.That(completed.Documents[0].DocumentKey, Is.EqualTo("DOC-NEW"));
        Assert.That(completed.Documents[0].DocumentName, Is.EqualTo("Updated name"));
    }

    private static StartMainframeListAggregationCommand CreateStartCommand(int totalExpected = 3) => new()
    {
        RequestId = $"REQ-LIST-{totalExpected}",
        PolicyNumber = "POL-001-TEST",
        RequestedAt = DateTimeOffset.UtcNow
    };

    private static MainframeAppraisalListPartReceivedEvent CreatePart(string requestId, int sequenceNumber, int totalExpected, string documentKey, string? documentName = null) => new()
    {
        RequestId = requestId,
        SequenceNumber = sequenceNumber,
        TotalExpected = totalExpected,
        Document = new Uc4DocumentSummary
        {
            DocumentId = $"ID-{documentKey}",
            DocumentKey = documentKey,
            SourceSystem = "Mainframe",
            DocumentType = "Appraisal",
            DocumentName = documentName ?? documentKey,
            DocumentDate = $"2026-05-0{sequenceNumber}",
            PolicyNumber = "POL-001-TEST",
            Status = "Available"
        }
    };
}

internal sealed class FakeArtemisAdapter : IArtemisAdapter
{
    public int ListRequests { get; private set; }

    public int DocumentRequests { get; private set; }

    public void SendListRequest(string requestId, string policyNumber)
        => ListRequests++;

    public void SendDocumentRequest(string requestId, string documentKey)
        => DocumentRequests++;
}
