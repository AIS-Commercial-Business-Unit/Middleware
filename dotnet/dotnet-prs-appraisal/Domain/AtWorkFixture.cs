using System.Text;

namespace dotnet_prs_appraisal.Domain;

internal static class AtWorkFixture
{
    private static readonly Dictionary<string, List<DocumentSummary>> Fixture = new(StringComparer.OrdinalIgnoreCase)
    {
        ["POL-001-TEST"] =
        [
            new DocumentSummary
            {
                DocumentId = "DOC-AW-001",
                DocumentKey = "DOC_RiskID_I_TEST001",
                SourceSystem = "AtWork",
                DocumentType = "Appraisal",
                DocumentName = "Insured Full Appraisal",
                DocumentDate = "2024-02-10",
                PolicyNumber = "POL-001-TEST",
                Status = "Available"
            },
            new DocumentSummary
            {
                DocumentId = "DOC-AW-002",
                DocumentKey = "DOC_RiskID_A_TEST002",
                SourceSystem = "AtWork",
                DocumentType = "Appraisal",
                DocumentName = "Agent Appraisal Report",
                DocumentDate = "2024-01-20",
                PolicyNumber = "POL-001-TEST",
                Status = "Available"
            }
        ],
        ["POL-002-TEST"] = [],
        ["POL-003-TEST"] =
        [
            new DocumentSummary
            {
                DocumentId = "DOC-AW-100",
                DocumentKey = "DOC_RiskID_I_TEST100",
                SourceSystem = "AtWork",
                DocumentType = "Reinspection",
                DocumentName = "Reinspection Report",
                DocumentDate = "2024-04-05",
                PolicyNumber = "POL-003-TEST",
                Status = "Available"
            }
        ]
    };

    public static List<DocumentSummary> GetDocuments(string policyNumber)
    {
        if (Fixture.TryGetValue(policyNumber, out var documents))
        {
            return documents.Select(Clone).ToList();
        }

        return
        [
            new DocumentSummary
            {
                DocumentId = "DOC-AW-DEFAULT",
                DocumentKey = "DOC_RiskID_I_DEFAULT",
                SourceSystem = "AtWork",
                DocumentType = "Appraisal",
                DocumentName = "Default Appraisal Document",
                DocumentDate = "2024-01-01",
                PolicyNumber = policyNumber,
                Status = "Available"
            }
        ];
    }

    public static DocumentRetrievalResult BuildRetrievalResult(string requestId, string documentKey)
    {
        var document = Fixture.Values
            .SelectMany(static docs => docs)
            .FirstOrDefault(doc => string.Equals(doc.DocumentKey, documentKey, StringComparison.OrdinalIgnoreCase));

        var documentId = document?.DocumentId ?? $"DOC-AW-{documentKey}";
        var fileName = $"appraisal-{documentKey}.pdf";
        var payload = $"%PDF-1.4\n% UC4 AtWork Fixture\nDocumentKey: {documentKey}\nGenerated: 2026-05-31\n%%EOF";

        return new DocumentRetrievalResult
        {
            RequestId = requestId,
            DocumentId = documentId,
            DocumentKey = documentKey,
            SourceSystem = "AtWork",
            ContentType = "application/pdf",
            ContentBase64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload)),
            FileName = fileName
        };
    }

    private static DocumentSummary Clone(DocumentSummary document) => new()
    {
        DocumentId = document.DocumentId,
        DocumentKey = document.DocumentKey,
        SourceSystem = document.SourceSystem,
        DocumentType = document.DocumentType,
        DocumentName = document.DocumentName,
        DocumentDate = document.DocumentDate,
        PolicyNumber = document.PolicyNumber,
        Status = document.Status
    };
}
