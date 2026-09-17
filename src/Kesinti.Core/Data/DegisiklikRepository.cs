using Dapper;
using Kesinti.Core.Models;

namespace Kesinti.Core.Data;

public sealed class DegisiklikRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public DegisiklikRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<int> EkleAsync(DegisiklikKaydi kayit)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            INSERT INTO dbo.DegisiklikKaydi (Sistem, DegisiklikTipi, Aciklama, PlanlananTarih, KaynakDosya)
            OUTPUT INSERTED.DegisiklikKaydiId
            VALUES (@Sistem, @DegisiklikTipi, @Aciklama, @PlanlananTarih, @KaynakDosya)
            """;
        return await connection.ExecuteScalarAsync<int>(sql, kayit);
    }
}
