using MailRelay.Service.Models;
using MailRelay.Service.Options;
using Microsoft.Data.SqlClient;

namespace MailRelay.Service.Data;

public sealed class RelaySettingsRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public RelaySettingsRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<RelaySettings?> GetAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Enabled, Host, Port, EnableSsl, Username, Password, FromAddress, FromDisplayName,
                   MaxConcurrentSend, UpdatedAtUtc, UpdatedBy
            FROM dbo.RelaySettings
            WHERE Id = 1;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct))
            return null;

        return new RelaySettings
        {
            Enabled = reader.GetBoolean(0),
            Host = reader.GetString(1),
            Port = reader.GetInt32(2),
            EnableSsl = reader.GetBoolean(3),
            Username = reader.IsDBNull(4) ? null : reader.GetString(4),
            Password = reader.IsDBNull(5) ? null : reader.GetString(5),
            FromAddress = reader.GetString(6),
            FromDisplayName = reader.IsDBNull(7) ? null : reader.GetString(7),
            MaxConcurrentSend = reader.GetInt32(8),
            UpdatedAtUtc = reader.GetDateTime(9),
            UpdatedBy = reader.IsDBNull(10) ? null : reader.GetString(10),
        };
    }

    // Id=1 satiri yoksa appsettings.json > SmtpSettings degerleriyle olusturur (ilk kurulum).
    public async Task EnsureSeedAsync(SmtpOptions seed, CancellationToken ct = default)
    {
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM dbo.RelaySettings WHERE Id = 1)
            BEGIN
                INSERT INTO dbo.RelaySettings
                    (Id, Enabled, Host, Port, EnableSsl, Username, Password, FromAddress, FromDisplayName, MaxConcurrentSend)
                VALUES
                    (1, @Enabled, @Host, @Port, @EnableSsl, @Username, @Password, @FromAddress, @FromDisplayName, 4);
            END
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Enabled", seed.Enabled);
        command.Parameters.AddWithValue("@Host", string.IsNullOrWhiteSpace(seed.Host) ? "smtp.local" : seed.Host);
        command.Parameters.AddWithValue("@Port", seed.Port <= 0 ? 25 : seed.Port);
        command.Parameters.AddWithValue("@EnableSsl", seed.EnableSsl);
        command.Parameters.AddWithValue("@Username", (object?)(string.IsNullOrWhiteSpace(seed.Username) ? null : seed.Username) ?? DBNull.Value);
        command.Parameters.AddWithValue("@Password", (object?)(string.IsNullOrWhiteSpace(seed.Password) ? null : seed.Password) ?? DBNull.Value);
        command.Parameters.AddWithValue("@FromAddress", string.IsNullOrWhiteSpace(seed.FromAddress) ? "noreply@example.com" : seed.FromAddress);
        command.Parameters.AddWithValue("@FromDisplayName", (object?)(string.IsNullOrWhiteSpace(seed.FromDisplayName) ? null : seed.FromDisplayName) ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }

    public async Task UpdateAsync(RelaySettingsUpdateRequest request, string? updatedBy, CancellationToken ct = default)
    {
        // Parola bos/null gelirse mevcut deger korunur (COALESCE ile).
        const string sql = """
            UPDATE dbo.RelaySettings
            SET Enabled = @Enabled,
                Host = @Host,
                Port = @Port,
                EnableSsl = @EnableSsl,
                Username = @Username,
                Password = COALESCE(@Password, Password),
                FromAddress = @FromAddress,
                FromDisplayName = @FromDisplayName,
                MaxConcurrentSend = @MaxConcurrentSend,
                UpdatedAtUtc = SYSUTCDATETIME(),
                UpdatedBy = @UpdatedBy
            WHERE Id = 1;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Enabled", request.Enabled);
        command.Parameters.AddWithValue("@Host", request.Host);
        command.Parameters.AddWithValue("@Port", request.Port);
        command.Parameters.AddWithValue("@EnableSsl", request.EnableSsl);
        command.Parameters.AddWithValue("@Username", (object?)request.Username ?? DBNull.Value);
        command.Parameters.AddWithValue("@Password", (object?)(string.IsNullOrEmpty(request.Password) ? null : request.Password) ?? DBNull.Value);
        command.Parameters.AddWithValue("@FromAddress", request.FromAddress);
        command.Parameters.AddWithValue("@FromDisplayName", (object?)request.FromDisplayName ?? DBNull.Value);
        command.Parameters.AddWithValue("@MaxConcurrentSend", request.MaxConcurrentSend <= 0 ? 4 : request.MaxConcurrentSend);
        command.Parameters.AddWithValue("@UpdatedBy", (object?)updatedBy ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(ct);
    }
}
