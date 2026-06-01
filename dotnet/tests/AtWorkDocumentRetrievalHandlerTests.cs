using dotnet_prs_appraisal.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using Middleware.Contracts.Events;
using NServiceBus.Testing;
using NUnit.Framework;

namespace Middleware.Tests;

[TestFixture]
public sealed class AtWorkDocumentRetrievalHandlerTests
{
    [Test]
    public async Task WhenRetrievalRequested_HandlerPublishesAtWorkDocumentRetrievedEvent()
    {
        var handler = new AtWorkDocumentRetrievalHandler(NullLogger<AtWorkDocumentRetrievalHandler>.Instance);
        var context = new TestableMessageHandlerContext();

        await handler.Handle(new Uc4AppraisalDocumentRetrievalRequestedEvent
        {
            RequestId = "REQ-ATWORK-1",
            PolicyNumber = "POL-001-TEST",
            DocumentKey = "DOC_RiskID_I_TEST001"
        }, context);

        var published = context.PublishedMessages
            .Select(m => m.Message<Uc4AtWorkDocumentRetrievedEvent>())
            .SingleOrDefault(m => m is not null);

        Assert.That(published, Is.Not.Null);
        Assert.That(published!.RequestId, Is.EqualTo("REQ-ATWORK-1"));
        Assert.That(published.DocumentKey, Is.EqualTo("DOC_RiskID_I_TEST001"));
        Assert.That(published.MimeType, Is.EqualTo("application/pdf"));
        Assert.That(published.Content, Is.Not.Empty);
    }

    [Test]
    public async Task WhenRetrievalRequested_HandlerPopulatesContentBase64()
    {
        var handler = new AtWorkDocumentRetrievalHandler(NullLogger<AtWorkDocumentRetrievalHandler>.Instance);
        var context = new TestableMessageHandlerContext();

        await handler.Handle(new Uc4AppraisalDocumentRetrievalRequestedEvent
        {
            RequestId = "REQ-ATWORK-2",
            PolicyNumber = "POL-003-TEST",
            DocumentKey = "DOC_RiskID_I_TEST100"
        }, context);

        var published = context.PublishedMessages
            .Select(m => m.Message<Uc4AtWorkDocumentRetrievedEvent>())
            .SingleOrDefault(m => m is not null);

        Assert.That(published, Is.Not.Null);
        // Content must be valid base64
        Assert.DoesNotThrow(() => Convert.FromBase64String(published!.Content));
    }
}
