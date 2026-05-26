using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;
using dotnet_kafka_bridge.Infrastructure;

namespace dotnet_kafka_bridge.Handlers;

public sealed class FileBatchEventHandler : IHandleMessages<FileBatchCompletedEvent>
{
    public async Task Handle(FileBatchCompletedEvent message, IMessageHandlerContext context)
    {
        const string topic = "file.events.file-batch-completed";
        using (LogContext.PushProperty("batchId", message.BatchId))
        {
            KafkaBridgeRuntime.Logger?.LogInformation(
                "KafkaBridge forwarding FileBatchCompletedEvent — batchId={BatchId} topic={Topic} recordCount={RecordCount}",
                message.BatchId, topic, message.RecordCount);
        }

        await KafkaBridgeRuntime.PublishAsync(topic, message, context.CancellationToken).ConfigureAwait(false);

        using (LogContext.PushProperty("batchId", message.BatchId))
        {
            KafkaBridgeRuntime.Logger?.LogInformation(
                "KafkaBridge event forwarded — batchId={BatchId} topic={Topic}",
                message.BatchId, topic);
        }
    }
}
