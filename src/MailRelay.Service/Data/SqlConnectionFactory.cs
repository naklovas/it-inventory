using Microsoft.Data.SqlClient;

namespace MailRelay.Service.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("MailDb")
            ?? throw new InvalidOperationException("appsettings.json: ConnectionStrings:MailDb bos birakilamaz.");
    }

    public async Task<SqlConnection> OpenAsync(CancellationToken ct = default)
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        return connection;
    }
}
