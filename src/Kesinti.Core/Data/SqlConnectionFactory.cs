using Kesinti.Core.Configuration;
using Microsoft.Data.SqlClient;

namespace Kesinti.Core.Data;

public sealed class SqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(DatabaseOptions options)
    {
        _connectionString = options.ConnectionString;
    }

    public SqlConnection Create()
    {
        var connection = new SqlConnection(_connectionString);
        connection.Open();
        return connection;
    }
}
