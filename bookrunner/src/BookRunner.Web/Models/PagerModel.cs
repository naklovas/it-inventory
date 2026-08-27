namespace BookRunner.Web.Models;

/// <summary>Sayfalama bagi. Mevcut filtre degerlerini koruyarak sayfa degistirir.</summary>
/// <param name="CurrentPage">Su anki sayfa.</param>
/// <param name="TotalPages">Toplam sayfa sayisi.</param>
/// <param name="RouteValues">Baglantilarda korunacak filtre parametreleri.</param>
public sealed record PagerModel(
    int CurrentPage,
    int TotalPages,
    Dictionary<string, string> RouteValues);
