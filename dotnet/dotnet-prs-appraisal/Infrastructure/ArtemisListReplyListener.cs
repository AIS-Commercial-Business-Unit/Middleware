using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Apache.NMS;
using Apache.NMS.ActiveMQ;
using Middleware.Contracts.Events;
using Middleware.Contracts.Models;
using NServiceBus;
using Serilog.Context;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed partial class ArtemisListReplyListener : BackgroundService
{
    private static readonly Regex SequenceRegex = SequenceLine();

    private readonly IConfiguration _configuration;
    private readonly IMessageSession _messageSession;
    private readonly ILogger<ArtemisListReplyListener> _logger;
    private readonly ConcurrentDictionary<string, SequenceAccumulator> _pendingDocuments = new(StringComparer.OrdinalIgnoreCase);

    public ArtemisListReplyListener(
        IConfiguration configuration,
        IMessageSession messageSession,
        ILogger<ArtemisListReplyListener> logger)
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

                _logger.LogInformation("Listening for Artemis list replies on {QueueName}.", GetReplyQueue());

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
                _logger.LogError(ex, "Artemis list reply listener failed. Retrying in 5 seconds.");
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
            _logger.LogWarning("Skipping Artemis list reply without correlation id.");
            return;
        }

        var parsed = ParseDocument(message.Text);
        LogEdaFlow(correlationId, "MqListReply", "Mainframe", "PrsAppraisal", "APPRAISAL.LIST.REPLY", "consumed");
        var accumulator = _pendingDocuments.GetOrAdd(correlationId, _ => new SequenceAccumulator(parsed.Total));
        accumulator.ExpectedTotal = parsed.Total;
        accumulator.Documents[parsed.Sequence] = parsed.Document;

        if (accumulator.Documents.Count < accumulator.ExpectedTotal)
        {
            return;
        }

        if (_pendingDocuments.TryRemove(correlationId, out var completed))
        {
            var documents = completed.Documents
                .OrderBy(static item => item.Key)
                .Select(static item => item.Value)
                .ToList();

            LogEdaFlow(correlationId, "Uc4MainframeDocumentListCompletedEvent", "PrsAppraisal", "DocumentListSaga", "nsb.uc4mainframedocumentlistcompleted", "published");
            await _messageSession.Publish(
                    new Uc4MainframeDocumentListCompletedEvent
                    {
                        RequestId = correlationId,
                        Documents = documents
                    },
                    cancellationToken)
                .ConfigureAwait(false);
        }
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

    private static (int Sequence, int Total, Uc4DocumentSummary Document) ParseDocument(string body)
    {
        var lines = body
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (lines.Length == 0)
        {
            throw new InvalidOperationException("Artemis list reply body was empty.");
        }

        var match = SequenceRegex.Match(lines[0]);
        if (!match.Success)
        {
            throw new InvalidOperationException($"Invalid list reply sequence header: '{lines[0]}'.");
        }

        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in lines.Skip(1))
        {
            var separator = line.IndexOf('=');
            if (separator <= 0)
            {
                continue;
            }

            var key = line[..separator].Trim();
            var value = line[(separator + 1)..].Trim();
            values[key] = value;
        }

        return (
            Sequence: int.Parse(match.Groups[1].Value),
            Total: int.Parse(match.Groups[2].Value),
            Document: new Uc4DocumentSummary
            {
                DocumentId = GetValue(values, "APPRAISAL_UID"),
                DocumentKey = GetValue(values, "DOCUMENTKEY"),
                SourceSystem = "Mainframe",
                DocumentType = GetValue(values, "DOCUMENTTYPE"),
                DocumentName = GetValue(values, "DOCUMENTNAME"),
                DocumentDate = GetValue(values, "APPRAISAL_DTE"),
                PolicyNumber = GetValue(values, "POLICY_QUOTE_NBR"),
                Status = "Available"
            });
    }

    private static string GetValue(IReadOnlyDictionary<string, string> values, string key) => values.TryGetValue(key, out var value) ? value : string.Empty;

    private string GetBrokerUrl() => _configuration["Artemis:BrokerUrl"] ?? "activemq:tcp://activemq-artemis:61616";
    private string GetUser() => _configuration["Artemis:User"] ?? string.Empty;
    private string GetPassword() => _configuration["Artemis:Password"] ?? string.Empty;
    private string GetReplyQueue() => _configuration["Artemis:ListReplyQueue"] ?? "APPRAISAL.LIST.REPLY";

    private sealed class SequenceAccumulator
    {
        public SequenceAccumulator(int expectedTotal)
        {
            ExpectedTotal = expectedTotal;
        }

        public int ExpectedTotal { get; set; }

        public ConcurrentDictionary<int, Uc4DocumentSummary> Documents { get; } = new();
    }

    [GeneratedRegex("^SEQUENCE=(\\d+) OF (\\d+)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SequenceLine();
}
