namespace Kesinti.Risk.Risk;

/// <summary>
/// LiteLLM'e gonderilen JSON Schema. Model, SADECE kendisine sunulan gecmis kesinti kayitlarina
/// (aday listesine) dayanarak risk seviyesi belirler; listede olmayan kesinti ID'si uretemez.
/// </summary>
public static class RiskAssessmentSchema
{
    public static object Build(IReadOnlyList<int> gecerliKesintiIdleri)
    {
        return new
        {
            type = "object",
            properties = new
            {
                riskSeviyesi = new
                {
                    type = "string",
                    @enum = new[] { "Dusuk", "Orta", "Yuksek" },
                    description = "Degisikligin gecmis kesintilere benzerligine dayanan risk seviyesi.",
                },
                dayanilanKesintiIdleri = new
                {
                    type = "array",
                    items = new
                    {
                        type = "integer",
                        @enum = gecerliKesintiIdleri,
                    },
                    description = "Degerlendirmeye dayanak olan gecmis KesintiKayitId degerleri; sadece verilen aday listesinden secilir.",
                },
                onerilenOnlemler = new
                {
                    type = "string",
                    description = "Gecmis kesintilerden ogrenilen, dayanak kayitlarla dogrulanabilir onerilen onlemler.",
                },
            },
            required = new[] { "riskSeviyesi", "dayanilanKesintiIdleri", "onerilenOnlemler" },
            additionalProperties = false,
        };
    }
}
