using dotnet_prs_appraisal.Sagas;
using Microsoft.Extensions.Logging.Abstractions;
using Middleware.Contracts.Commands;
using Middleware.Contracts.Events;
using Middleware.Contracts.Messages;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class DocumentRetrievalSagaTests
{
    // ── AtWork path ──────────────────────────────────────────────────────────────

    [Test]
    public async Task WhenAtWorkRequest_SagaPublishesRetrievalRequestedEventAndDoesNotReplyYet()
    {
        var saga = new DocumentRetrievalSaga(NullLogger<DocumentRetrievalSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(CreateAtWorkCommand("REQ-DOC-AW-1", "DOC_RiskID_I_TEST001"), context);

        var retrievalEvent = context.PublishedMessages
            .Select(m => m.Message<Uc4AppraisalDocumentRetrievalRequestedEvent>())
            .SingleOrDefault(m => m is not null);

        Assert.That(retrievalEvent, Is.Not.Null, "Saga must publish Uc4AppraisalDocumentRetrievalRequestedEvent");
        Assert.That(retrievalEvent!.RequestId, Is.EqualTo("REQ-DOC-AW-1"));
        Assert.That(retrievalEvent.DocumentKey, Is.EqualTo("DOC_RiskID_I_TEST001"));
        Assert.That(saga.Data.AtWorkPending, Is.True, "AtWorkPending must be set");
        Assert.That(context.RepliedMessages, Is.Empty, "Saga must not reply until async result arrives");
    }

    [Test]
    public async Task WhenAtWorkRetrievedEventArrives_SagaRepliesWithContent()
    {
        var saga = new DocumentRetrievalSaga(NullLogger<DocumentRetrievalSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(CreateAtWorkCommand("REQ-DOC-AW-2", "DOC_RiskID_I_TEST001"), context);

        // Simulate AtWorkDocumentRetrievalHandler response
        await saga.Handle(new Uc4AtWorkDocumentRetrievedEvent
        {
            RequestId = "REQ-DOC-AW-2",
            DocumentKey = "DOC_RiskID_I_TEST001",
            Content = "AAAA",
            MimeType = "application/pdf"
        }, context);

        Assert.That(saga.Data.AtWorkDone, Is.True, "AtWorkDone must be set after receiving result");
        var reply = context.RepliedMessages
            .Select(m => m.Message<RetrieveAppraisalDocumentResponse>())
            .SingleOrDefault(m => m is not null);

        Assert.That(reply, Is.Not.Null, "Saga must reply after AtWork result arrives");
        Assert.That(reply!.RequestId, Is.EqualTo("REQ-DOC-AW-2"));
        Assert.That(reply.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(reply.ContentBase64, Is.EqualTo("AAAA"));
        Assert.That(reply.SourceSystem, Is.EqualTo("AtWork"));
    }

    [Test]
    public async Task WhenAtWorkRequestWithSourceSystemHeader_SagaRoutesToAtWorkPath()
    {
        var saga = new DocumentRetrievalSaga(NullLogger<DocumentRetrievalSaga>.Instance);
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
            .Select(m => m.Message<Uc4AppraisalDocumentRetrievalRequestedEvent>())
            .Any(m => m is not null), Is.True);
    }

    // ── Mainframe path ───────────────────────────────────────────────────────────

    [Test]
    public async Task WhenMainframeRequest_SagaSendsAggregationCommandAndDoesNotReplyYet()
    {
        var saga = new DocumentRetrievalSaga(NullLogger<DocumentRetrievalSaga>.Instance);
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

        Assert.That(sent, Is.Not.Null, "Saga must send StartMainframeDocumentAggregationCommand");
        Assert.That(sent!.RequestId, Is.EqualTo("REQ-DOC-MF-1"));
        Assert.That(context.RepliedMessages, Is.Empty, "Saga must not reply until Mainframe result arrives");
    }

    [Test]
    public async Task WhenMainframeDocumentRetrievedEventArrives_SagaRepliesWithContent()
    {
        var saga = new DocumentRetrievalSaga(NullLogger<DocumentRetrievalSaga>.Instance);
        var context = new TestableMessageHandlerContext();

        await saga.Handle(new RetrieveAppraisalDocumentCommand
        {
            RequestId = "REQ-DOC-MF-2",
            DocumentKey = "MF-DOC-KEY",
            SourceSystem = "Mainframe",
            RequestedAt = DateTimeOffset.UtcNow
        }, context);

        await saga.Handle(new Uc4AppraisalDocumentRetrievedEvent
        {
            RequestId = "REQ-DOC-MF-2",
            DocumentKey = "MF-DOC-KEY",
            SourceSystem = "Mainframe",
            ContentType = "application/pdf",
            ContentBase64 = "BBBB",
            FileName = "mainframe-doc.pdf"
        }, context);

        var reply = context.RepliedMessages
            .Select(m => m.Message<RetrieveAppraisalDocumentResponse>())
            .SingleOrDefault(m => m is not null);

        Assert.That(reply, Is.Not.Null);
        Assert.That(reply!.RequestId, Is.EqualTo("REQ-DOC-MF-2"));
        Assert.That(reply.ContentBase64, Is.EqualTo("BBBB"));
        Assert.That(reply.SourceSystem, Is.EqualTo("Mainframe"));
    }

    private static RetrieveAppraisalDocumentCommand CreateAtWorkCommand(string requestId, string documentKey) => new()
    {
        RequestId = requestId,
        DocumentKey = documentKey,
        SourceSystem = "AtWork",
        RequestedAt = DateTimeOffset.UtcNow
    };
}
