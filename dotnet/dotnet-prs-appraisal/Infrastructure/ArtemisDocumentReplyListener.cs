using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Middleware.Contracts.Events;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class ArtemisDocumentReplyListener : BackgroundService
{
    private const string EndOfDocumentSentinel = "||END-OF-DOCUMENT||";

    private readonly IConfiguration _configuration;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<ArtemisDocumentReplyListener> _logger;

    public ArtemisDocumentReplyListener(
        IConfiguration configuration,
        IMessageSession messageSession,
        ILogger<ArtemisDocumentReplyListener> logger)
    {
        _configuration = configuration;
        _messageSession = messageSession;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            IConnection? connection = null;
            Apache.NMS.ISession? session = null;
            IMessageConsumer? consumer = null;

            try
            {
                var factory = new ConnectionFactory(GetBrokerUrl());
                connection = string.IsNullOrWhiteSpace(GetUser())
                    ? factory.CreateConnection()
                    : factory.CreateConnection(GetUser(), GetPassword());

                connection.Start();
                session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
                consumer = session.CreateConsumer(session.GetQueue(GetReplyQueue()));

                _logger.LogInformation("Listening for Artemis document replies on {QueueName}.", GetReplyQueue());

                while (!stoppingToken.IsCancellationRequested)
                {
                    var message = consumer.Receive(TimeSpan.FromSeconds(1));
                    if (message is not ITextMessage textMessage)
                    {
                        await Task.Delay(100, stoppingToken).ConfigureAwait(false);
                        continue;
                    }

                    await ProcessMessageAsync(textMessage, stoppingToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Artemis document reply listener failed. Retrying in 5 seconds.");
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
            }
            finally
            {
                try { consumer?.Close(); } catch { }
                try { session?.Close(); } catch { }
                try { connection?.Close(); } catch { }
            }
        }
    }

    private async Task ProcessMessageAsync(ITextMessage message, CancellationToken cancellationToken)
    {
        var correlationId = message.NMSCorrelationID;
        if (string.IsNullOrWhiteSpace(correlationId))
        {
            _logger.LogWarning("Skipping Artemis document reply without correlation id.");
            return;
        }

        var text = (message.Text ?? string.Empty).Replace("\r", string.Empty).Replace("\n", string.Empty);
        var isFinalChunk = text.Contains(EndOfDocumentSentinel, StringComparison.Ordinal);
        var chunk = text.Replace(EndOfDocumentSentinel, string.Empty, StringComparison.Ordinal);

        LogEdaFlow(correlationId, "MqDocumentChunk", "Mainframe", "MainframeDocumentAggregator", "APPRAISAL.DOCUMENT.REPLY", "consumed");
        await _messageSession.Publish(
                new MainframeDocumentChunkReceivedEvent
                {
                    RequestId = correlationId,
                    ChunkPayload = chunk,
                    IsFinal = isFinalChunk
                },
                cancellationToken)
            .ConfigureAwait(false);
    }

    private void LogEdaFlow(string requestId, string messageType, string from, string to, string topic, string direction = "consumed")
    {
        using var _1 = LogContext.PushProperty("EDA_Event", "EDA_FLOW");
        using var _2 = LogContext.PushProperty("EDA_IssuanceId", requestId);
        using var _3 = LogContext.PushProperty("EDA_MessageType", messageType);
        using var _4 = LogContext.PushProperty("EDA_From", from);
        using var _5 = LogContext.PushProperty("EDA_To", to);
        using var _6 = LogContext.PushProperty("EDA_Direction", direction);
        using var _7 = LogContext.PushProperty("EDA_Stack", "dotnet");
        using var _8 = LogContext.PushProperty("EDA_Topic", topic);
        _logger.LogInformation("EDA_FLOW {EDA_MessageType} {EDA_From} -> {EDA_To}", messageType, from, to);
    }

    private string GetBrokerUrl() => _configuration["Artemis:BrokerUrl"] ?? "activemq:tcp://activemq-artemis:61616";
    private string GetUser() => _configuration["Artemis:User"] ?? string.Empty;
    private string GetPassword() => _configuration["Artemis:Password"] ?? string.Empty;
    private string GetReplyQueue() => _configuration["Artemis:DocumentReplyQueue"] ?? "APPRAISAL.DOCUMENT.REPLY";
}
