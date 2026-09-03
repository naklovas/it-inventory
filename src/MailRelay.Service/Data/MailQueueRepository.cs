using MailRelay.Service.Models;
using Microsoft.Data.SqlClient;

namespace MailRelay.Service.Data;

// Kuyruk + gonderim gecmisi (log) uzerindeki tum veritabani erisimi. Ham ADO.NET kullanilir
// (repodaki FisSayilariRepository ile ayni tarz) - ek bir ORM bagimliligi eklenmez.
public sealed class MailQueueRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public MailQueueRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<long> InsertAsync(MailQueueItem item, List<MailAttachmentInput>? attachments, CancellationToken ct = default)
    {
        const string insertSql = """
            INSERT INTO dbo.MailQueue
                (ClientApplicationId, RequestedByUsername, RequestedByTeam, ToAddresses, CcAddresses, BccAddresses,
                 Subject, Body, IsBodyHtml, Priority, Status, Attempts, MaxAttempts, NextAttemptAtUtc, CorrelationId, SourcePort)
            OUTPUT INSERTED.Id
            VALUES
                (@ClientApplicationId, @RequestedByUsername, @RequestedByTeam, @ToAddresses, @CcAddresses, @BccAddresses,
                 @Subject, @Body, @IsBodyHtml, @Priority, @Status, 0, @MaxAttempts, NULL, @CorrelationId, @SourcePort);
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        long newId;
        await using (var command = new SqlCommand(insertSql, connection, transaction))
        {
            command.Parameters.AddWithValue("@ClientApplicationId", (object?)item.ClientApplicationId ?? DBNull.Value);
            command.Parameters.AddWithValue("@RequestedByUsername", (object?)item.RequestedByUsername ?? DBNull.Value);
            command.Parameters.AddWithValue("@RequestedByTeam", (object?)item.RequestedByTeam ?? DBNull.Value);
            command.Parameters.AddWithValue("@ToAddresses", item.ToAddresses);
            command.Parameters.AddWithValue("@CcAddresses", (object?)item.CcAddresses ?? DBNull.Value);
            command.Parameters.AddWithValue("@BccAddresses", (object?)item.BccAddresses ?? DBNull.Value);
            command.Parameters.AddWithValue("@Subject", item.Subject);
            command.Parameters.AddWithValue("@Body", item.Body);
            command.Parameters.AddWithValue("@IsBodyHtml", item.IsBodyHtml);
            command.Parameters.AddWithValue("@Priority", item.Priority);
            command.Parameters.AddWithValue("@Status", MailStatus.Queued);
            command.Parameters.AddWithValue("@MaxAttempts", item.MaxAttempts);
            command.Parameters.AddWithValue("@CorrelationId", (object?)item.CorrelationId ?? DBNull.Value);
            command.Parameters.AddWithValue("@SourcePort", (object?)item.SourcePort ?? DBNull.Value);

            newId = (long)(await command.ExecuteScalarAsync(ct))!;
        }

        if (attachments is { Count: > 0 })
        {
            const string attachmentSql = """
                INSERT INTO dbo.MailAttachments (MailQueueId, FileName, ContentType, Content)
                VALUES (@MailQueueId, @FileName, @ContentType, @Content);
                """;

            foreach (var attachment in attachments)
            {
                await using var command = new SqlCommand(attachmentSql, connection, transaction);
                command.Parameters.AddWithValue("@MailQueueId", newId);
                command.Parameters.AddWithValue("@FileName", attachment.FileName);
                command.Parameters.AddWithValue("@ContentType", (object?)attachment.ContentType ?? DBNull.Value);
                command.Parameters.AddWithValue("@Content", Convert.FromBase64String(attachment.ContentBase64));
                await command.ExecuteNonQueryAsync(ct);
            }
        }

        await transaction.CommitAsync(ct);
        return newId;
    }

    // Bir kaydi gonderim icin atomik olarak "Processing" durumuna cekmeye calisir. Ayni kayit
    // hem kanal sinyaliyle hem periyodik DB taramasiyla tetiklenebildiginden, birden fazla worker
    // (hatta birden fazla servis kopyasi) ayni kaydi ele gecirmeye calissa bile UPDATE'in satir
    // kilidi sayesinde yalnizca biri kazanir; digerleri 0 satir gunceller ve null doner.
    public async Task<MailQueueItem?> TryClaimAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.MailQueue
            SET Status = @Processing
            OUTPUT INSERTED.Id, INSERTED.ClientApplicationId, INSERTED.RequestedByUsername, INSERTED.RequestedByTeam,
                   INSERTED.ToAddresses, INSERTED.CcAddresses, INSERTED.BccAddresses, INSERTED.Subject, INSERTED.Body,
                   INSERTED.IsBodyHtml, INSERTED.Priority, INSERTED.Status, INSERTED.Attempts, INSERTED.MaxAttempts,
                   INSERTED.NextAttemptAtUtc, INSERTED.LastError, INSERTED.CorrelationId, INSERTED.SourcePort,
                   INSERTED.CreatedAtUtc, INSERTED.SentAtUtc
            WHERE Id = @Id AND Status IN (@Queued, @Retrying);
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Processing", MailStatus.Processing);
        command.Parameters.AddWithValue("@Queued", MailStatus.Queued);
        command.Parameters.AddWithValue("@Retrying", MailStatus.Retrying);

        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return Map(reader);
    }

    // Yeniden baslatma sonrasi kurtarma ve zamanlanmis retry'lar icin: gonderilmeyi bekleyen
    // kayitlarin id'lerini tarar. READPAST + ROWLOCK, ayni tabloyu tarayan baska bir worker/servis
    // kopyasinin kilitli satirlarini atlayarak coklu ornek (multi-instance) altinda da guvenli calisir.
    public async Task<List<long>> PollClaimableIdsAsync(int batchSize, CancellationToken ct = default)
    {
        const string sql = """
            SELECT TOP (@BatchSize) Id
            FROM dbo.MailQueue WITH (READPAST, ROWLOCK)
            WHERE Status IN (@Queued, @Retrying)
              AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= SYSUTCDATETIME())
            ORDER BY Priority ASC, CreatedAtUtc ASC;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@BatchSize", batchSize);
        command.Parameters.AddWithValue("@Queued", MailStatus.Queued);
        command.Parameters.AddWithValue("@Retrying", MailStatus.Retrying);

        var ids = new List<long>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            ids.Add(reader.GetInt64(0));

        return ids;
    }

    public async Task MarkSentAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.MailQueue
            SET Status = @Sent, SentAtUtc = SYSUTCDATETIME(), LastError = NULL
            WHERE Id = @Id;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Sent", MailStatus.Sent);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkRetryAsync(long id, string error, DateTime nextAttemptUtc, int attempts, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.MailQueue
            SET Status = @Retrying, Attempts = @Attempts, NextAttemptAtUtc = @NextAttemptAtUtc,
                LastError = @LastError
            WHERE Id = @Id;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Retrying", MailStatus.Retrying);
        command.Parameters.AddWithValue("@Attempts", attempts);
        command.Parameters.AddWithValue("@NextAttemptAtUtc", nextAttemptUtc);
        command.Parameters.AddWithValue("@LastError", Truncate(error, 2000));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task MarkFailedAsync(long id, string error, int attempts, CancellationToken ct = default)
    {
        const string sql = """
            UPDATE dbo.MailQueue
            SET Status = @Failed, Attempts = @Attempts, LastError = @LastError
            WHERE Id = @Id;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Failed", MailStatus.Failed);
        command.Parameters.AddWithValue("@Attempts", attempts);
        command.Parameters.AddWithValue("@LastError", Truncate(error, 2000));
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task<MailQueueItem?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, ClientApplicationId, RequestedByUsername, RequestedByTeam, ToAddresses, CcAddresses,
                   BccAddresses, Subject, Body, IsBodyHtml, Priority, Status, Attempts, MaxAttempts,
                   NextAttemptAtUtc, LastError, CorrelationId, SourcePort, CreatedAtUtc, SentAtUtc
            FROM dbo.MailQueue
            WHERE Id = @Id;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<List<MailAttachmentRecord>> GetAttachmentsAsync(long mailQueueId, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, MailQueueId, FileName, ContentType, Content
            FROM dbo.MailAttachments
            WHERE MailQueueId = @MailQueueId;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@MailQueueId", mailQueueId);

        var results = new List<MailAttachmentRecord>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            results.Add(new MailAttachmentRecord
            {
                Id = reader.GetInt64(0),
                MailQueueId = reader.GetInt64(1),
                FileName = reader.GetString(2),
                ContentType = reader.IsDBNull(3) ? null : reader.GetString(3),
                Content = (byte[])reader[4],
            });
        }

        return results;
    }

    public async Task<PagedResult<MailQueueItem>> SearchAsync(MailLogSearchFilter filter, CancellationToken ct = default)
    {
        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 200);

        var where = new List<string>();
        var parameters = new List<SqlParameter>();

        if (!string.IsNullOrWhiteSpace(filter.Status))
        {
            where.Add("Status = @Status");
            parameters.Add(new SqlParameter("@Status", filter.Status));
        }

        if (!string.IsNullOrWhiteSpace(filter.RequestedByUsername))
        {
            where.Add("RequestedByUsername = @RequestedByUsername");
            parameters.Add(new SqlParameter("@RequestedByUsername", filter.RequestedByUsername));
        }

        if (!string.IsNullOrWhiteSpace(filter.RequestedByTeam))
        {
            where.Add("RequestedByTeam = @RequestedByTeam");
            parameters.Add(new SqlParameter("@RequestedByTeam", filter.RequestedByTeam));
        }

        if (filter.FromUtc is { } fromUtc)
        {
            where.Add("CreatedAtUtc >= @FromUtc");
            parameters.Add(new SqlParameter("@FromUtc", fromUtc));
        }

        if (filter.ToUtc is { } toUtc)
        {
            where.Add("CreatedAtUtc <= @ToUtc");
            parameters.Add(new SqlParameter("@ToUtc", toUtc));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            where.Add("(Subject LIKE @SearchText OR ToAddresses LIKE @SearchText OR Body LIKE @SearchText OR CorrelationId LIKE @SearchText)");
            parameters.Add(new SqlParameter("@SearchText", $"%{filter.SearchText}%"));
        }

        var whereClause = where.Count > 0 ? "WHERE " + string.Join(" AND ", where) : "";

        var countSql = $"SELECT COUNT(*) FROM dbo.MailQueue {whereClause};";
        var pageSql = $"""
            SELECT Id, ClientApplicationId, RequestedByUsername, RequestedByTeam, ToAddresses, CcAddresses,
                   BccAddresses, Subject, Body, IsBodyHtml, Priority, Status, Attempts, MaxAttempts,
                   NextAttemptAtUtc, LastError, CorrelationId, SourcePort, CreatedAtUtc, SentAtUtc
            FROM dbo.MailQueue
            {whereClause}
            ORDER BY CreatedAtUtc DESC
            OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);

        long totalCount;
        await using (var countCommand = new SqlCommand(countSql, connection))
        {
            foreach (var p in parameters)
                countCommand.Parameters.Add(CloneParameter(p));
            totalCount = (int)(await countCommand.ExecuteScalarAsync(ct))!;
        }

        var items = new List<MailQueueItem>();
        await using (var pageCommand = new SqlCommand(pageSql, connection))
        {
            foreach (var p in parameters)
                pageCommand.Parameters.Add(CloneParameter(p));
            pageCommand.Parameters.AddWithValue("@Offset", (page - 1) * pageSize);
            pageCommand.Parameters.AddWithValue("@PageSize", pageSize);

            await using var reader = await pageCommand.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
                items.Add(Map(reader));
        }

        return new PagedResult<MailQueueItem>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        };
    }

    private static SqlParameter CloneParameter(SqlParameter source) => new(source.ParameterName, source.Value);

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];

    private static MailQueueItem Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt64(0),
        ClientApplicationId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
        RequestedByUsername = reader.IsDBNull(2) ? null : reader.GetString(2),
        RequestedByTeam = reader.IsDBNull(3) ? null : reader.GetString(3),
        ToAddresses = reader.GetString(4),
        CcAddresses = reader.IsDBNull(5) ? null : reader.GetString(5),
        BccAddresses = reader.IsDBNull(6) ? null : reader.GetString(6),
        Subject = reader.GetString(7),
        Body = reader.GetString(8),
        IsBodyHtml = reader.GetBoolean(9),
        Priority = reader.GetByte(10),
        Status = reader.GetString(11),
        Attempts = reader.GetInt32(12),
        MaxAttempts = reader.GetInt32(13),
        NextAttemptAtUtc = reader.IsDBNull(14) ? null : reader.GetDateTime(14),
        LastError = reader.IsDBNull(15) ? null : reader.GetString(15),
        CorrelationId = reader.IsDBNull(16) ? null : reader.GetString(16),
        SourcePort = reader.IsDBNull(17) ? null : reader.GetInt32(17),
        CreatedAtUtc = reader.GetDateTime(18),
        SentAtUtc = reader.IsDBNull(19) ? null : reader.GetDateTime(19),
    };
}
