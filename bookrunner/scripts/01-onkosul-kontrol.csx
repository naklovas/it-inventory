// Gecis oncesi onkosullari kontrol eden ornek script.
// Gorev detayindaki "calistir" dugmesiyle tetiklenir.

Log($"Runbook: {RunbookCode} - {RunbookTitle}");
Log($"Gorev: {TaskTitle}");
Log($"Calistiran: {ExecutedBy}");

var ortam = Param("ortam", "TEST");
Log($"Hedef ortam: {ortam}");

var kontroller = new List<(string Ad, bool Sonuc)>
{
    ("Bakim penceresi acik", DateTimeOffset.Now.Hour is >= 22 or <= 5),
    ("Ortam parametresi verildi", !string.IsNullOrWhiteSpace(ortam)),
    ("Uretim onayi", !ortam.Equals("PROD", StringComparison.OrdinalIgnoreCase)
                     || Param("onay") == "evet")
};

foreach (var (ad, sonuc) in kontroller)
{
    Log($"{(sonuc ? "[OK]  " : "[HATA]")} {ad}");
}

var basarisiz = kontroller.Count(k => !k.Sonuc);

// Script'in dondurdugu deger sonuc ekraninda ve audit kaydinda gorunur.
return basarisiz == 0
    ? "Tum onkosullar saglandi."
    : $"{basarisiz} onkosul saglanmadi; goreve baslamayin.";
