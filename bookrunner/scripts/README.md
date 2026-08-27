# Ornek CSX script'leri

Bu klasordeki `.csx` dosyalari, BookRunner'in Roslyn C# Scripting (CSX)
calistiricisina ornektir. Script'ler yonetim ekranindan veya API'den
(`POST /api/scripts`) veritabanina kaydedilir ve gorevlere baglanir.

Script icinde niteliksiz olarak kullanabileceginiz uyeler
(`ScriptGlobals` sinifi):

| Uye | Aciklama |
| --- | --- |
| `RunbookCode`, `RunbookTitle` | Baglamdaki runbook |
| `TaskTitle` | Baglamdaki gorev (varsa) |
| `ExecutedBy` | Script'i calistiran Windows hesabi |
| `Parameters` | Calistirma sirasinda gecilen parametreler |
| `Param("ad", "varsayilan")` | Tek bir parametreyi okur |
| `Log("mesaj")` | Sonuc ekranina ve audit'e yazilan cikti satiri |

Guvenlik notu: script'ler uygulama sureci icinde tam guvenle calisir.
Bu nedenle script **yazma** yetkisi yalnizca `Administrator` rolundedir,
her calistirma audit trail'e yazilir ve `Scripting:BlockedPatterns`
listesindeki ifadeler reddedilir.
