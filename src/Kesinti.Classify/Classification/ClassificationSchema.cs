namespace Kesinti.Classify.Classification;

/// <summary>
/// LiteLLM'e gonderilen JSON Schema tanimi. Kategori kapali listeden secilir (enum);
/// eksik alanlar sadece dokumanda gecen bilgiden doldurulur, model uydurmaz.
/// </summary>
public static class ClassificationSchema
{
    public static object Build(IReadOnlyList<string> kategoriAdlari)
    {
        return new
        {
            type = "object",
            properties = new
            {
                kategori = new
                {
                    type = "string",
                    @enum = kategoriAdlari,
                    description = "Verilen kapali listeden secilen kategori adi.",
                },
                tarih = new
                {
                    type = new[] { "string", "null" },
                    description = "ISO 8601 (yyyy-MM-ddTHH:mm:ss) formatinda kesinti tarihi; ham metinde acikca yoksa null.",
                },
                sureDakika = new
                {
                    type = new[] { "integer", "null" },
                    description = "Kesinti suresi dakika olarak; ham metinde acikca yoksa null.",
                },
                etkilenenSistemler = new
                {
                    type = new[] { "string", "null" },
                    description = "Etkilenen sistemler; ham metinde acikca yoksa null.",
                },
                neden = new
                {
                    type = new[] { "string", "null" },
                    description = "Kesinti nedeni; ham metinde acikca yoksa null.",
                },
                alinanOnlemler = new
                {
                    type = new[] { "string", "null" },
                    description = "Alinan onlemler; ham metinde acikca yoksa null.",
                },
                gerekce = new
                {
                    type = "string",
                    description = "Kategori seciminin kisa gerekcesi.",
                },
            },
            required = new[] { "kategori", "tarih", "sureDakika", "etkilenenSistemler", "neden", "alinanOnlemler", "gerekce" },
            additionalProperties = false,
        };
    }
}
