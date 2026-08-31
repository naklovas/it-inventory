namespace BookRunner.Application.Common;

/// <summary>
/// Oyunlastirma puan degerleri (appsettings: "Gamification"). Kod degistirmeden
/// puan agirliklarini ayarlayabilmek icin disaridan yapilandirilir. Bir deger
/// sonradan degisse bile, daha once verilmis GamificationEvent kayitlari
/// (ve dolayisiyla o ana kadarki toplam puanlar) etkilenmez.
/// </summary>
public sealed class GamificationOptions
{
    public const string SectionName = "Gamification";

    public bool Enabled { get; set; } = true;

    /// <summary>Bir gorev tamamlandiginda taban puan.</summary>
    public int TaskCompletionPoints { get; set; } = 10;

    /// <summary>Yuksek oncelikli gorevlerde taban puanin carpani.</summary>
    public double HighPriorityMultiplier { get; set; } = 1.5;

    /// <summary>Kritik oncelikli gorevlerde taban puanin carpani.</summary>
    public double CriticalPriorityMultiplier { get; set; } = 2.0;

    /// <summary>Gorev, planlanan bitis tarihinden once/tam zamaninda kapatilirsa ek puan.</summary>
    public int OnTimeBonusPoints { get; set; } = 5;

    /// <summary>Gorev "Basarisiz" olarak kapanirsa (geri alma/hata) puan kirilir.</summary>
    public int TaskFailedPenaltyPoints { get; set; } = -5;

    /// <summary>Runbook'un tum gorevleri tamamlanip kapandiginda sahibine verilen puan.</summary>
    public int RunbookCompletionPoints { get; set; } = 50;

    /// <summary>Bir goreve yorum/not birakildiginda verilen puan (katilimi tesvik icin).</summary>
    public int CommentPoints { get; set; } = 1;
}
