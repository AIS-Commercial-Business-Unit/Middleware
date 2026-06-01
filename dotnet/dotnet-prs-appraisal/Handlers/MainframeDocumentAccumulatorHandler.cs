using System.Text;
using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Events;
using NServiceBus;

namespace dotnet_prs_appraisal.Handlers;

public sealed class MainframeDocumentAccumulatorHandler : IHandleMessages<MainframeDocumentChunkReceivedEvent>
{
    private readonly IAccumulatorRepository _accumulatorRepository;
    private readonly IDocumentRetrievalRequestRepository _requestRepository;
    private readonly ILogger<MainframeDocumentAccumulatorHandler> _logger;

    public MainframeDocumentAccumulatorHandler(
        IAccumulatorRepository accumulatorRepository,
        IDocumentRetrievalRequestRepository requestRepository,
        ILogger<MainframeDocumentAccumulatorHandler> logger)
    {
        _accumulatorRepository = accumulatorRepository;
        _requestRepository = requestRepository;
        _logger = logger;
    }

    public async Task Handle(MainframeDocumentChunkReceivedEvent message, IMessageHandlerContext context)
    {
        await _accumulatorRepository.CreateDocumentChunkAsync(
            message.RequestId,
            message.ChunkPayload,
            message.IsFinal,
            context.CancellationToken).ConfigureAwait(false);

        if (!message.IsFinal)
        {
            return;
        }

        var (won, assembledContent) = await _accumulatorRepository
            .TryCompleteDocumentAsync(message.RequestId, context.CancellationToken)
            .ConfigureAwait(false);

        if (!won)
        {
            return;
        }

        var request = await _requestRepository.FindAsync(message.RequestId).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No document retrieval request record found for {message.RequestId}.");

        var contentBase64 = string.IsNullOrEmpty(assembledContent)
            ? string.Empty
            : Convert.ToBase64String(Encoding.UTF8.GetBytes(assembledContent));

        _logger.LogInformation(
            "Document accumulation completed for {RequestId} and document {DocumentKey}.",
            message.RequestId,
            request.DocumentKey);

        await context.Publish(new MainframeDocumentAccumulationCompleteEvent
        {
            RequestId = message.RequestId,
            DocumentKey = request.DocumentKey,
            ContentBase64 = contentBase64
        }).ConfigureAwait(false);
    }
}
