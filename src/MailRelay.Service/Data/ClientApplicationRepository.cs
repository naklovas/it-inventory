using MailRelay.Service.Models;
using Microsoft.Data.SqlClient;

namespace MailRelay.Service.Data;

public sealed class ClientApplicationRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public ClientApplicationRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<ClientApplication?> FindByApiKeyAsync(string apiKey, CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, ApiKey, Enabled, CreatedAtUtc
            FROM dbo.ClientApplications
            WHERE ApiKey = @ApiKey AND Enabled = 1;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@ApiKey", apiKey);

        await using var reader = await command.ExecuteReaderAsync(ct);
        return await reader.ReadAsync(ct) ? Map(reader) : null;
    }

    public async Task<List<ClientApplication>> GetAllAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT Id, Name, ApiKey, Enabled, CreatedAtUtc
            FROM dbo.ClientApplications
            ORDER BY Name;
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);

        var results = new List<ClientApplication>();
        await using var reader = await command.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
            results.Add(Map(reader));

        return results;
    }

    public async Task<ClientApplication> CreateAsync(string name, string apiKey, CancellationToken ct = default)
    {
        const string sql = """
            INSERT INTO dbo.ClientApplications (Name, ApiKey, Enabled)
            OUTPUT INSERTED.Id, INSERTED.Name, INSERTED.ApiKey, INSERTED.Enabled, INSERTED.CreatedAtUtc
            VALUES (@Name, @ApiKey, 1);
            """;

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Name", name);
        command.Parameters.AddWithValue("@ApiKey", apiKey);

        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return Map(reader);
    }

    public async Task<bool> SetEnabledAsync(int id, bool enabled, CancellationToken ct = default)
    {
        const string sql = "UPDATE dbo.ClientApplications SET Enabled = @Enabled WHERE Id = @Id;";

        await using var connection = await _connectionFactory.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.AddWithValue("@Id", id);
        command.Parameters.AddWithValue("@Enabled", enabled);
        return await command.ExecuteNonQueryAsync(ct) > 0;
    }

    private static ClientApplication Map(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32(0),
        Name = reader.GetString(1),
        ApiKey = reader.GetString(2),
        Enabled = reader.GetBoolean(3),
        CreatedAtUtc = reader.GetDateTime(4),
    };
}
