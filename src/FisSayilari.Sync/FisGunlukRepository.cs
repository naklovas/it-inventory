using Microsoft.Data.SqlClient;

namespace FisSayilari.Sync;

public sealed class FisGunlukRepository
{
    private readonly string _connectionString;

    public FisGunlukRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(IReadOnlyList<GunlukFisSayisi> satirlar, CancellationToken ct = default)
    {
        if (satirlar.Count == 0) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        const string merge = """
            MERGE dbo.FisGunlukOzet AS hedef
            USING (SELECT @Tarih AS Tarih, @Kanal AS Kanal) AS kaynak
                ON hedef.Tarih = kaynak.Tarih AND hedef.Kanal = kaynak.Kanal
            WHEN MATCHED THEN
                UPDATE SET ToplamFisSayisi = @ToplamFisSayisi, GuncellemeZamani = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Tarih, Kanal, ToplamFisSayisi, GuncellemeZamani)
                VALUES (@Tarih, @Kanal, @ToplamFisSayisi, SYSUTCDATETIME());
            """;

        foreach (var satir in satirlar)
        {
            await using var command = new SqlCommand(merge, connection, transaction);
            command.Parameters.AddWithValue("@Tarih", satir.Gun.ToDateTime(TimeOnly.MinValue));
            command.Parameters.AddWithValue("@Kanal", satir.Kanal);
            command.Parameters.AddWithValue("@ToplamFisSayisi", satir.ToplamFisSayisi);
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
