using System.Security.Cryptography;
using System.Text;

namespace BookRunner.Application.Common;

/// <summary>
/// Foto bulunmayan kullanicilar/gruplar icin bas harf ve deterministik renk uretir.
/// Ayni kisi her ekranda ayni rengi alsin diye renk SID'den turetilir.
/// </summary>
public static class AvatarHelper
{
    /// <summary>Arayuzde birbirinden ayirt edilebilir, koyu zeminde okunur renk paleti.</summary>
    private static readonly string[] Palette =
    [
        "#4F86F7", "#E8734A", "#3FA796", "#B5559B", "#D9A404",
        "#5B6ABF", "#2E9E68", "#C0504D", "#7A5AA8", "#0F7C8A",
        "#D2691E", "#417505"
    ];

    /// <summary>Gorev barlari icin kullanilan, sirayla dagitilan renkler.</summary>
    public static readonly string[] TaskPalette =
    [
        "#4F86F7", "#E8734A", "#3FA796", "#B5559B", "#D9A404",
        "#5B6ABF", "#2E9E68", "#C0504D", "#7A5AA8", "#0F7C8A"
    ];

    /// <summary>Sira numarasina gore gorev bari rengi secer.</summary>
    public static string TaskColor(int order)
        => TaskPalette[Math.Abs(order - 1) % TaskPalette.Length];

    /// <summary>"Ahmet Yilmaz" -> "AY". Tek kelime ise ilk iki harf.</summary>
    public static string Initials(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            return "?";
        }

        var parts = displayName.Split([' ', '.', '-', '_'], StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => "?",
            1 => parts[0].Length >= 2
                ? parts[0][..2].ToUpperInvariant()
                : parts[0].ToUpperInvariant(),
            _ => string.Concat(char.ToUpperInvariant(parts[0][0]), char.ToUpperInvariant(parts[^1][0]))
        };
    }

    /// <summary>Anahtardan (SID/ad) deterministik palet rengi secer.</summary>
    public static string Color(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return Palette[0];
        }

        var hash = MD5.HashData(Encoding.UTF8.GetBytes(key));
        return Palette[hash[0] % Palette.Length];
    }
}
