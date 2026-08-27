namespace BookRunner.Application.Security;

/// <summary>
/// Runbook bazli yetki karari verir.
///
/// Kural: bir islemi ya rolun izni ya da <b>runbook sahipligi</b> acar.
/// Runbook'u olusturan kisi (sahibi) kendi runbook'u uzerinde her degisikligi
/// yapabilir; yonetici rolu ise tum izinlere sahip oldugu icin her runbook'ta
/// ayni yetkiye sahiptir.
/// </summary>
public interface IRunbookAccess
{
    /// <summary>Runbook baglami olmayan islemler icin yalnizca rol iznine bakar.</summary>
    /// <exception cref="Common.ForbiddenException">Yetki yoksa.</exception>
    void Ensure(string permission);

    /// <summary>Rol izni yoksa runbook sahipligine bakar.</summary>
    /// <exception cref="Common.ForbiddenException">Ikisi de yoksa.</exception>
    Task EnsureForRunbookAsync(Guid runbookId, string permission, CancellationToken ct = default);

    /// <summary>Gorevin bagli oldugu runbook uzerinden ayni karari verir.</summary>
    /// <exception cref="Common.ForbiddenException">Yetki de sahiplik de yoksa.</exception>
    Task EnsureForTaskAsync(Guid taskId, string permission, CancellationToken ct = default);

    /// <summary>Oturum acan kullanici bu runbook'un sahibi mi.</summary>
    Task<bool> IsOwnerOfRunbookAsync(Guid runbookId, CancellationToken ct = default);

    /// <summary>Oturum acan kullanici bu gorevin bagli oldugu runbook'un sahibi mi.</summary>
    Task<bool> IsOwnerOfTaskAsync(Guid taskId, CancellationToken ct = default);
}
