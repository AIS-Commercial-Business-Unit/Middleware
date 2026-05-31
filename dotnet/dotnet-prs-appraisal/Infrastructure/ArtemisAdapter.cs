using Apache.NMS;
using Apache.NMS.ActiveMQ;

namespace dotnet_prs_appraisal.Infrastructure;

public sealed class ArtemisAdapter : IArtemisAdapter, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<ArtemisAdapter> _logger;
    private readonly object _syncRoot = new();

    private IConnection? _connection;
    private Apache.NMS.ISession? _session;
    private IMessageProducer? _listProducer;
    private IMessageProducer? _documentProducer;

    public ArtemisAdapter(IConfiguration configuration, ILogger<ArtemisAdapter> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    public void SendListRequest(string requestId, string policyNumber)
    {
        Send(requestId, $"APPRAISAL_LIST|||{policyNumber}|||ACTIVE|||", GetListRequestQueue(), isDocumentRequest: false);
    }

    public void SendDocumentRequest(string requestId, string documentKey)
    {
        Send(requestId, $"APPRAISAL_DOC|||{documentKey}|||", GetDocumentRequestQueue(), isDocumentRequest: true);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            DisposeConnection();
        }
    }

    private void Send(string requestId, string payload, string queueName, bool isDocumentRequest)
    {
        lock (_syncRoot)
        {
            try
            {
                EnsureConnected();

                var session = _session ?? throw new InvalidOperationException("Artemis session is not initialized.");
                var producer = isDocumentRequest
                    ? _documentProducer ?? throw new InvalidOperationException("Document producer is not initialized.")
                    : _listProducer ?? throw new InvalidOperationException("List producer is not initialized.");

                var message = session.CreateTextMessage(payload);
                message.NMSCorrelationID = requestId;
                producer.Send(message);

                _logger.LogInformation(
                    "Sent Artemis {RequestType} request to {QueueName} with correlation {CorrelationId}.",
                    isDocumentRequest ? "document" : "list",
                    queueName,
                    requestId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send Artemis request with correlation {CorrelationId}.", requestId);
                DisposeConnection();
                throw;
            }
        }
    }

    private void EnsureConnected()
    {
        if (_connection is not null && _session is not null && _listProducer is not null && _documentProducer is not null)
        {
            return;
        }

        var factory = new ConnectionFactory(GetBrokerUrl());
        _connection = string.IsNullOrWhiteSpace(GetUser())
            ? factory.CreateConnection()
            : factory.CreateConnection(GetUser(), GetPassword());

        _connection.Start();
        _session = _connection.CreateSession(AcknowledgementMode.AutoAcknowledge);
        _listProducer = _session.CreateProducer(_session.GetQueue(GetListRequestQueue()));
        _documentProducer = _session.CreateProducer(_session.GetQueue(GetDocumentRequestQueue()));
    }

    private void DisposeConnection()
    {
        try { _listProducer?.Close(); } catch { }
        try { _documentProducer?.Close(); } catch { }
        try { _session?.Close(); } catch { }
        try { _connection?.Close(); } catch { }

        _listProducer = null;
        _documentProducer = null;
        _session = null;
        _connection = null;
    }

    private string GetBrokerUrl() => _configuration["Artemis:BrokerUrl"] ?? "activemq:tcp://activemq-artemis:61616";
    private string GetUser() => _configuration["Artemis:User"] ?? string.Empty;
    private string GetPassword() => _configuration["Artemis:Password"] ?? string.Empty;
    private string GetListRequestQueue() => _configuration["Artemis:ListRequestQueue"] ?? "APPRAISAL.LIST.REQUEST";
    private string GetDocumentRequestQueue() => _configuration["Artemis:DocumentRequestQueue"] ?? "APPRAISAL.DOCUMENT.REQUEST";
}
