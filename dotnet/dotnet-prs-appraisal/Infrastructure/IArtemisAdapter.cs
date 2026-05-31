namespace dotnet_prs_appraisal.Infrastructure;

public interface IArtemisAdapter
{
    void SendListRequest(string requestId, string policyNumber);

    void SendDocumentRequest(string requestId, string documentKey);
}
