namespace dotnet_file_processing.Services;

public sealed class BatchProcessingStatus
{
    public int ProcessedRecords { get; set; }
    public int SucceededRecords { get; set; }
    public int FailedRecords { get; set; }
    public bool IsComplete(int totalRecords) =>
        totalRecords > 0 && ProcessedRecords >= totalRecords;
}

public interface IFileBatchProgressManager
{
    Task<BatchProcessingStatus> GetProgressAsync(string batchId, CancellationToken cancellationToken);
}
