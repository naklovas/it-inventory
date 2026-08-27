// Runbook baglamindan basit bir ozet metni ureten ornek script.
// Cikti, gorev tarihcesine ve audit trail'e yazilir.

var satirlar = new List<string>
{
    $"Runbook  : {RunbookCode} - {RunbookTitle}",
    $"Gorev    : {TaskTitle ?? "(gorev baglami yok)"}",
    $"Zaman    : {DateTimeOffset.Now:dd.MM.yyyy HH:mm}",
    $"Calistiran: {ExecutedBy}"
};

foreach (var parametre in Parameters)
{
    satirlar.Add($"Parametre: {parametre.Key} = {parametre.Value}");
}

foreach (var satir in satirlar)
{
    Log(satir);
}

return string.Join(" | ", satirlar);
