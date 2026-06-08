using dotnet_prs_appraisal.Infrastructure;
using Middleware.Contracts.Events;
using NServiceBus;

namespace dotnet_prs_appraisal.Handlers;

public sealed class MainframeListAccumulatorHandler : IHandleMessages<MainframeAppraisalListPartReceivedEvent>
{
    private readonly IAccumulatorRepository _accumulatorRepository;
    private readonly ILogger<MainframeListAccumulatorHandler> _logger;

    public MainframeListAccumulatorHandler(
        IAccumulatorRepository accumulatorRepository,
        ILogger<MainframeListAccumulatorHandler> logger)
    {
        _accumulatorRepository = accumulatorRepository;
        _logger = logger;
    }

    public async Task Handle(MainframeAppraisalListPartReceivedEvent message, IMessageHandlerContext context)
    {
        await _accumulatorRepository.CreateListPartAsync(
            message.RequestId,
            message.SequenceNumber,
            message.TotalExpected,
            message.Document,
            context.CancellationToken).ConfigureAwait(false);

        var (won, documents) = await _accumulatorRepository
            .TryCompleteListAsync(message.RequestId, context.CancellationToken)
            .ConfigureAwait(false);

        if (!won)
        {
            return;
        }

        _logger.LogInformation(
            "List accumulation completed for {RequestId} with {DocumentCount} documents.",
            message.RequestId,
            documents.Count);

        await context.Publish(new MainframeListAccumulationCompleteEvent
        {
            RequestId = message.RequestId,
            Documents = documents
        }).ConfigureAwait(false);
    }
}
