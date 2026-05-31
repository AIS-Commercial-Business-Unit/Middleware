namespace dotnet_prs_appraisal.Infrastructure;

public interface IAppraisalSagaRepository
{
    Task UpsertAsync(AppraisalSagaRecord record);
    Task<AppraisalSagaRecord?> GetByAppraisalIdAsync(string appraisalId);
    Task<IEnumerable<AppraisalSagaRecord>> GetByStatusAsync(string status);
    Task<IEnumerable<AppraisalSagaRecord>> GetAllActiveAsync();
}
