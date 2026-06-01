using System.Text;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Middleware.Contracts.Models;

namespace dotnet_prs_appraisal.Infrastructure;

public interface IAccumulatorRepository
{
    Task EnsureCreatedAsync(CancellationToken cancellationToken = default);

    Task CreateListPartAsync(
        string requestId,
        int sequenceNumber,
        int totalExpected,
        AppraisalDocumentSummary document,
        CancellationToken cancellationToken = default);

    Task<(bool won, List<AppraisalDocumentSummary> documents)> TryCompleteListAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task<List<AppraisalDocumentSummary>> GetListDocumentsAsync(
        string requestId,
        CancellationToken cancellationToken = default);

    Task CreateDocumentChunkAsync(
        string requestId,
        string chunkPayload,
        bool isFinal,
        CancellationToken cancellationToken = default);

    Task<(bool won, string assembledContent)> TryCompleteDocumentAsync(
        string requestId,
        CancellationToken cancellationToken = default);
}

public sealed class AccumulatorRepository : IAccumulatorRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly string _connectionString;

    public AccumulatorRepository(string connectionString)
    {
        _connectionString = !string.IsNullOrWhiteSpace(connectionString)
            ? connectionString
            : throw new ArgumentException("A SQL connection string is required.", nameof(connectionString));
    }

    public async Task EnsureCreatedAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF OBJECT_ID(N'dbo.mf_list_headers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.mf_list_headers
                (
                    RequestId NVARCHAR(200) NOT NULL CONSTRAINT PK_mf_list_headers PRIMARY KEY,
                    TotalExpected INT NOT NULL,
                    CompletedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_mf_list_headers_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID(N'dbo.mf_list_parts', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.mf_list_parts
                (
                    RequestId NVARCHAR(200) NOT NULL,
                    SequenceNumber INT NOT NULL,
                    DocumentJson NVARCHAR(MAX) NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_mf_list_parts_CreatedAt DEFAULT SYSUTCDATETIME(),
                    CONSTRAINT PK_mf_list_parts PRIMARY KEY (RequestId, SequenceNumber)
                );
            END;

            IF OBJECT_ID(N'dbo.mf_document_headers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.mf_document_headers
                (
                    RequestId NVARCHAR(200) NOT NULL CONSTRAINT PK_mf_document_headers PRIMARY KEY,
                    CompletedAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_mf_document_headers_CreatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF OBJECT_ID(N'dbo.mf_document_chunks', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.mf_document_chunks
                (
                    ChunkIndex BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_mf_document_chunks PRIMARY KEY,
                    RequestId NVARCHAR(200) NOT NULL,
                    ChunkPayload NVARCHAR(MAX) NOT NULL,
                    IsFinal BIT NOT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_mf_document_chunks_CreatedAt DEFAULT SYSUTCDATETIME()
                );

                CREATE INDEX IX_mf_document_chunks_RequestId_ChunkIndex
                    ON dbo.mf_document_chunks (RequestId, ChunkIndex);
            END;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateListPartAsync(
        string requestId,
        int sequenceNumber,
        int totalExpected,
        AppraisalDocumentSummary document,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);
        ArgumentNullException.ThrowIfNull(document);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.mf_list_headers
            SET TotalExpected = CASE WHEN TotalExpected < @totalExpected THEN @totalExpected ELSE TotalExpected END
            WHERE RequestId = @requestId;

            IF @@ROWCOUNT = 0
            BEGIN
                BEGIN TRY
                    INSERT INTO dbo.mf_list_headers (RequestId, TotalExpected, CompletedAt)
                    VALUES (@requestId, @totalExpected, NULL);
                END TRY
                BEGIN CATCH
                    IF ERROR_NUMBER() NOT IN (2601, 2627)
                        THROW;

                    UPDATE dbo.mf_list_headers
                    SET TotalExpected = CASE WHEN TotalExpected < @totalExpected THEN @totalExpected ELSE TotalExpected END
                    WHERE RequestId = @requestId;
                END CATCH
            END;

            UPDATE dbo.mf_list_parts
            SET DocumentJson = @documentJson
            WHERE RequestId = @requestId
              AND SequenceNumber = @sequenceNumber;

            IF @@ROWCOUNT = 0
            BEGIN
                BEGIN TRY
                    INSERT INTO dbo.mf_list_parts (RequestId, SequenceNumber, DocumentJson)
                    VALUES (@requestId, @sequenceNumber, @documentJson);
                END TRY
                BEGIN CATCH
                    IF ERROR_NUMBER() NOT IN (2601, 2627)
                        THROW;

                    UPDATE dbo.mf_list_parts
                    SET DocumentJson = @documentJson
                    WHERE RequestId = @requestId
                      AND SequenceNumber = @sequenceNumber;
                END CATCH
            END;
            """;
        command.Parameters.AddWithValue("@requestId", requestId);
        command.Parameters.AddWithValue("@sequenceNumber", sequenceNumber);
        command.Parameters.AddWithValue("@totalExpected", totalExpected);
        command.Parameters.AddWithValue("@documentJson", JsonSerializer.Serialize(document, JsonOptions));

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(bool won, List<AppraisalDocumentSummary> documents)> TryCompleteListAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        var totalExpected = await GetListTotalExpectedAsync(connection, requestId, cancellationToken).ConfigureAwait(false);
        if (totalExpected <= 0)
        {
            return (false, []);
        }

        var receivedCount = await GetListPartCountAsync(connection, requestId, cancellationToken).ConfigureAwait(false);
        if (receivedCount < totalExpected)
        {
            return (false, []);
        }

        await using var completeCommand = connection.CreateCommand();
        completeCommand.CommandText = """
            UPDATE dbo.mf_list_headers
            SET CompletedAt = SYSUTCDATETIME()
            WHERE RequestId = @requestId
              AND CompletedAt IS NULL;
            """;
        completeCommand.Parameters.AddWithValue("@requestId", requestId);

        var won = await completeCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        if (!won)
        {
            return (false, []);
        }

        return (true, await GetListDocumentsAsync(connection, requestId, cancellationToken).ConfigureAwait(false));
    }

    public async Task<List<AppraisalDocumentSummary>> GetListDocumentsAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        return await GetListDocumentsAsync(connection, requestId, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateDocumentChunkAsync(
        string requestId,
        string chunkPayload,
        bool isFinal,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = """
            UPDATE dbo.mf_document_headers
            SET RequestId = RequestId
            WHERE RequestId = @requestId;

            IF @@ROWCOUNT = 0
            BEGIN
                BEGIN TRY
                    INSERT INTO dbo.mf_document_headers (RequestId, CompletedAt)
                    VALUES (@requestId, NULL);
                END TRY
                BEGIN CATCH
                    IF ERROR_NUMBER() NOT IN (2601, 2627)
                        THROW;
                END CATCH
            END;

            INSERT INTO dbo.mf_document_chunks (RequestId, ChunkPayload, IsFinal)
            VALUES (@requestId, @chunkPayload, @isFinal);
            """;
        command.Parameters.AddWithValue("@requestId", requestId);
        command.Parameters.AddWithValue("@chunkPayload", chunkPayload ?? string.Empty);
        command.Parameters.AddWithValue("@isFinal", isFinal);

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<(bool won, string assembledContent)> TryCompleteDocumentAsync(
        string requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(requestId);

        await using var connection = await OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using var completeCommand = connection.CreateCommand();
        completeCommand.CommandText = """
            UPDATE dbo.mf_document_headers
            SET CompletedAt = SYSUTCDATETIME()
            WHERE RequestId = @requestId
              AND CompletedAt IS NULL;
            """;
        completeCommand.Parameters.AddWithValue("@requestId", requestId);

        var won = await completeCommand.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false) == 1;
        if (!won)
        {
            return (false, string.Empty);
        }

        await using var chunksCommand = connection.CreateCommand();
        chunksCommand.CommandText = """
            SELECT ChunkPayload
            FROM dbo.mf_document_chunks
            WHERE RequestId = @requestId
            ORDER BY ChunkIndex;
            """;
        chunksCommand.Parameters.AddWithValue("@requestId", requestId);

        var builder = new StringBuilder();
        await using var reader = await chunksCommand.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            builder.Append(reader.GetString(0));
        }

        return (true, builder.ToString());
    }

    private async Task<SqlConnection> OpenConnectionAsync(CancellationToken cancellationToken)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }

    private static async Task<int> GetListTotalExpectedAsync(SqlConnection connection, string requestId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT TOP (1) TotalExpected FROM dbo.mf_list_headers WHERE RequestId = @requestId;";
        command.Parameters.AddWithValue("@requestId", requestId);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is int totalExpected ? totalExpected : 0;
    }

    private static async Task<int> GetListPartCountAsync(SqlConnection connection, string requestId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM dbo.mf_list_parts WHERE RequestId = @requestId;";
        command.Parameters.AddWithValue("@requestId", requestId);

        var value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return value is int count ? count : Convert.ToInt32(value ?? 0);
    }

    private static async Task<List<AppraisalDocumentSummary>> GetListDocumentsAsync(
        SqlConnection connection,
        string requestId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DocumentJson
            FROM dbo.mf_list_parts
            WHERE RequestId = @requestId
            ORDER BY SequenceNumber;
            """;
        command.Parameters.AddWithValue("@requestId", requestId);

        var documents = new List<AppraisalDocumentSummary>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var documentJson = reader.GetString(0);
            var document = JsonSerializer.Deserialize<AppraisalDocumentSummary>(documentJson, JsonOptions);
            if (document is not null)
            {
                documents.Add(document);
            }
        }

        return documents;
    }
}
