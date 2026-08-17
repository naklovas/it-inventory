using Microsoft.Data.SqlClient;

namespace FisSayilari.Sync;

public sealed class FisSayilariRepository
{
    private readonly string _connectionString;

    public FisSayilariRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task UpsertAsync(IReadOnlyList<FisSayisiKaydi> satirlar, CancellationToken ct = default)
    {
        if (satirlar.Count == 0) return;

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);
        await using var transaction = connection.BeginTransaction();

        const string merge = """
            MERGE dbo.FisSayilariOzet AS hedef
            USING (SELECT @Zaman AS Zaman, @Kanal AS Kanal) AS kaynak
                ON hedef.Zaman = kaynak.Zaman AND hedef.Kanal = kaynak.Kanal
            WHEN MATCHED THEN
                UPDATE SET ToplamFisSayisi = @ToplamFisSayisi, GuncellemeZamani = SYSUTCDATETIME()
            WHEN NOT MATCHED THEN
                INSERT (Zaman, Kanal, ToplamFisSayisi, GuncellemeZamani)
                VALUES (@Zaman, @Kanal, @ToplamFisSayisi, SYSUTCDATETIME());
            """;

        foreach (var satir in satirlar)
        {
            await using var command = new SqlCommand(merge, connection, transaction);
            command.Parameters.AddWithValue("@Zaman", satir.Zaman);
            command.Parameters.AddWithValue("@Kanal", satir.Kanal);
            command.Parameters.AddWithValue("@ToplamFisSayisi", satir.ToplamFisSayisi);
            await command.ExecuteNonQueryAsync(ct);
        }

        await transaction.CommitAsync(ct);
    }
}
