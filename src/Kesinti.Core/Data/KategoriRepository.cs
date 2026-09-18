using Dapper;
using Kesinti.Core.Models;

namespace Kesinti.Core.Data;

public sealed class KategoriRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public KategoriRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<Kategori>> GetAktifKategorilerAsync()
    {
        using var connection = _connectionFactory.Create();
        var rows = await connection.QueryAsync<Kategori>(
            "SELECT KategoriId, Ad, Aciklama, AktifMi FROM dbo.Kategori WHERE AktifMi = 1 ORDER BY Ad");
        return rows.AsList();
    }

    public async Task<Kategori?> FindByAdAsync(string ad)
    {
        using var connection = _connectionFactory.Create();
        return await connection.QuerySingleOrDefaultAsync<Kategori>(
            "SELECT KategoriId, Ad, Aciklama, AktifMi FROM dbo.Kategori WHERE Ad = @ad",
            new { ad });
    }
}
