using Dapper;
using Kesinti.Core.Models;

namespace Kesinti.Core.Data;

public sealed class RiskRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public RiskRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> EkleAsync(RiskDegerlendirme degerlendirme)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            INSERT INTO dbo.RiskDegerlendirme
                (DegisiklikKaydiId, RiskSeviyesi, DayanilanKesintiIdleriJson, OnerilenOnlemler, ModelAdi, ModelCikisiJson)
            OUTPUT INSERTED.RiskDegerlendirmeId
            VALUES
                (@DegisiklikKaydiId, @RiskSeviyesi, @DayanilanKesintiIdleriJson, @OnerilenOnlemler, @ModelAdi, @ModelCikisiJson)
            """;
        return await connection.ExecuteScalarAsync<int>(sql, degerlendirme);
    }
}
