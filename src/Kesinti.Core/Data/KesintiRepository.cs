using Dapper;
using Kesinti.Core.Models;

namespace Kesinti.Core.Data;

public sealed class KesintiRepository
{
    private readonly SqlConnectionFactory _connectionFactory;

    public KesintiRepository(SqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<bool> DosyaZatenIslendiMiAsync(string dosyaHash)
    {
        using var connection = _connectionFactory.Create();
        var count = await connection.ExecuteScalarAsync<int>(
            "SELECT COUNT(1) FROM dbo.KesintiDokuman WHERE DosyaHash = @dosyaHash",
            new { dosyaHash });
        return count > 0;
    }

    public async Task<int> DokumanEkleAsync(KesintiDokuman dokuman)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            INSERT INTO dbo.KesintiDokuman (DosyaYolu, DosyaHash, HamMetin, FormatDurumu)
            OUTPUT INSERTED.KesintiDokumanId
            VALUES (@DosyaYolu, @DosyaHash, @HamMetin, @FormatDurumu)
            """;
        return await connection.ExecuteScalarAsync<int>(sql, dokuman);
    }

    public async Task<int> KayitEkleAsync(KesintiKayit kayit)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            INSERT INTO dbo.KesintiKayit
                (KesintiDokumanId, Tarih, SureDakika, EtkilenenSistemler, Neden, AlinanOnlemler, KategoriId, SiniflandirmaDurumu)
            OUTPUT INSERTED.KesintiKayitId
            VALUES
                (@KesintiDokumanId, @Tarih, @SureDakika, @EtkilenenSistemler, @Neden, @AlinanOnlemler, @KategoriId, @SiniflandirmaDurumu)
            """;
        return await connection.ExecuteScalarAsync<int>(sql, kayit);
    }

    public async Task<IReadOnlyList<KesintiKayit>> GetBekleyenKayitlarAsync()
    {
        using var connection = _connectionFactory.Create();
        var rows = await connection.QueryAsync<KesintiKayit>(
            "SELECT * FROM dbo.KesintiKayit WHERE SiniflandirmaDurumu = @durum",
            new { durum = SiniflandirmaDurumu.Bekliyor });
        return rows.AsList();
    }

    public async Task<KesintiDokuman?> GetDokumanAsync(int kesintiDokumanId)
    {
        using var connection = _connectionFactory.Create();
        return await connection.QuerySingleOrDefaultAsync<KesintiDokuman>(
            "SELECT * FROM dbo.KesintiDokuman WHERE KesintiDokumanId = @kesintiDokumanId",
            new { kesintiDokumanId });
    }

    public async Task<IReadOnlyList<KesintiKayit>> GetSiniflandirilmisKayitlarAsync()
    {
        using var connection = _connectionFactory.Create();
        var rows = await connection.QueryAsync<KesintiKayit>(
            "SELECT * FROM dbo.KesintiKayit WHERE SiniflandirmaDurumu = @durum",
            new { durum = SiniflandirmaDurumu.Siniflandirildi });
        return rows.AsList();
    }

    public async Task KayitGuncelleAsync(KesintiKayit kayit)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            UPDATE dbo.KesintiKayit
            SET Tarih = @Tarih,
                SureDakika = @SureDakika,
                EtkilenenSistemler = @EtkilenenSistemler,
                Neden = @Neden,
                AlinanOnlemler = @AlinanOnlemler,
                KategoriId = @KategoriId,
                SiniflandirmaDurumu = @SiniflandirmaDurumu
            WHERE KesintiKayitId = @KesintiKayitId
            """;
        await connection.ExecuteAsync(sql, kayit);
    }

    /// <summary>
    /// Siniflandirilmis kayitlar arasindan etkilenen sistemler alani verilen sistem adini
    /// iceren kayitlari getirir. Risk asamasinda "ayni sistem" filtresi icin kullanilir.
    /// </summary>
    public async Task<IReadOnlyList<KesintiKayit>> GetSistemeGoreSiniflandirilmisKayitlarAsync(string sistem)
    {
        using var connection = _connectionFactory.Create();
        var rows = await connection.QueryAsync<KesintiKayit>(
            """
            SELECT * FROM dbo.KesintiKayit
            WHERE SiniflandirmaDurumu = @durum
              AND EtkilenenSistemler LIKE '%' + @sistem + '%'
            """,
            new { durum = SiniflandirmaDurumu.Siniflandirildi, sistem });
        return rows.AsList();
    }

    public async Task<int> SiniflandirmaEkleAsync(KesintiSiniflandirma siniflandirma)
    {
        using var connection = _connectionFactory.Create();
        const string sql = """
            INSERT INTO dbo.KesintiSiniflandirma
                (KesintiKayitId, ModelAdi, ModelCikisiJson, OnerilenKategoriId, ElleDuzeltilmisKategoriId, ElleDuzeltenKullanici)
            OUTPUT INSERTED.KesintiSiniflandirmaId
            VALUES
                (@KesintiKayitId, @ModelAdi, @ModelCikisiJson, @OnerilenKategoriId, @ElleDuzeltilmisKategoriId, @ElleDuzeltenKullanici)
            """;
        return await connection.ExecuteScalarAsync<int>(sql, siniflandirma);
    }
}
