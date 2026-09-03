# MailRelay.Service

Uygulamalar icin ortak mail gonderme servisi. Cagiran uygulamalar mail'i kuyruga birakir,
servis kuyruktan tek bir relay hesabi uzerinden asenkron olarak gonderir; gonderilen (ve
gonderilemeyen) her mail dbo.MailQueue tablosunda kalici olarak loglanir. Admin panelinden
relay ayarlari yonetilir, mail loglari kullanici/takim bazli aranip raporlanabilir.

## Mimari ozet

```
İstemci uygulama(lar)  --X-Api-Key-->  POST /api/mail/send (5401/5402/5403)
                                              |
                                              v
                                     dbo.MailQueue (Status=Queued)  <-- kalicilik burada
                                              |
                              in-memory Channel (aninda sinyal)
                                              |
                                   MailQueueProcessor (N worker)
                                     - atomik "claim" (Queued/Retrying -> Processing)
                                     - RelaySettings (DB, cache'li) ile SMTP gonderimi
                                     - basarili: Sent | basarisiz: Retrying (backoff) / Failed
                                              |
                              +---------------+----------------+
                              |  ayrica periyodik DB taramasi (poll) - restart/retry kurtarma
                              +--------------------------------------------------------------

Admin panel (/admin)  --X-Admin-Key-->  /api/admin/*  (relay ayarlari, mail loglari, uygulamalar)
```

Kuyruk + log ayni tabloda (dbo.MailQueue) tutulur; Status alani yasam dongusunu temsil eder
(Queued -> Processing -> Sent | Retrying -> Failed). Ayni kayit, hem kanal sinyaliyle hem
periyodik taramayla iki kez tetiklense (ya da servis birden fazla ornek calistirilsa) bile
`TryClaimAsync` icindeki atomik `UPDATE ... WHERE Status IN (...)` sayesinde yalnizca bir
worker kaydi ele gecirir - cift gonderim olmaz.

**Coklu port:** appsettings.json > `Kestrel:Endpoints` altinda birden fazla port tanimlanmistir
(varsayilan 5401/5402/5403). Hepsi ayni API'yi sunar; her istekte hangi porttan geldigi
`MailQueue.SourcePort` alanina yazilir (izleme/raporlama icin). Ihtiyaca gore port sayisi
appsettings.json'da artirilip azaltilabilir.

**Performans/yuk:** `POST /api/mail/send` sadece DB'ye INSERT yapip 202 Accepted doner -
SMTP gonderimini beklemez. Gercek gonderim arka planda, `Queue:WorkerCount` kadar paralel
worker ile yapilir; relay hesabina ayni anda acilan baglanti sayisi da (worker sayisi ile)
sinirlandirilir. `PersonnelDirectory` sorgulari 10 dakika onbelleklenir, takim kataloğu ise
`TeamCatalogSyncMinutes` araligiyla arka planda senkronize edilir - gonderim yolu hicbir
zaman disari senkron cagri yapmaz (kullanici adi -> takim eslesmesi haric, o da hatada
sessizce null'a duser, gonderimi engellemez).

## Kurulum

1. **Veritabani:** `sql/mail_schema.sql` script'ini SQL Server uzerinde calistirin (tablolari
   olusturur, RelaySettings icin yer tutucu bir satir ekler).
2. **appsettings.json:**
   - `ConnectionStrings:MailDb` - SQL Server baglanti dizesi.
   - `Kestrel:Endpoints` - servisin dinleyecegi portlar.
   - `SmtpSettings` - **sadece ilk kurulumda** dbo.RelaySettings'e tohum (seed) olarak
     yazilir; sonrasinda gercek deger her zaman veritabanindan (admin panelinden) okunur.
     appsettings.json'daki degeri degistirmek calisma zamanindaki ayari ETKILEMEZ.
   - `PersonnelDirectory` - verilen ayarlarla birebir ayni (BaseUrl, LookupPathTemplate,
     TeamsPath, TeamCatalogSyncMinutes, TimeoutSeconds).
   - `Admin:ApiKey` - admin panel/API icin paylasimli anahtar. **Bos birakilirsa tum admin
     uc noktalari 503 doner** (guvenlik icin varsayilan olarak kapali).
3. **Calistirma:**
   ```bash
   cd src/MailRelay.Service
   dotnet run
   ```
4. **Ilk admin girisi:** Tarayicidan `http://<host>:5401/admin/` adresine gidip yukaridaki
   `Admin:ApiKey` degerini panelin sag ust kosesindeki alana girin (sadece bu sekmenin
   sessionStorage'inda tutulur). "Relay Ayarları" sekmesinden gercek SMTP relay bilgilerini
   girip kaydedin (appsettings.json'daki yer tutucu degerlerin uzerine yazilir).
5. **Istemci uygulama tanimlama:** Admin panelinde "Uygulamalar" sekmesinden her cagiran
   uygulama icin bir kayit olusturun; uretilen API anahtarini o uygulamanin konfigurasyonuna
   (guvenli sekilde) kaydedin - **anahtar yalnizca olusturma aninda gosterilir**.

## API kullanimi (istemci uygulamalar icin)

### Mail gonder

```bash
curl -X POST http://<host>:5401/api/mail/send \
  -H "X-Api-Key: <admin panelinden alinan anahtar>" \
  -H "Content-Type: application/json" \
  -d '{
        "to": ["kullanici@sirket.com"],
        "cc": ["yonetici@sirket.com"],
        "subject": "Talep onaylandi",
        "body": "<b>Merhaba</b>, talebiniz onaylandi.",
        "isBodyHtml": true,
        "requestedByUsername": "visikhan",
        "priority": 3,
        "correlationId": "talep-1234"
      }'
```

Yanit (kuyruga alindi, henuz gonderilmedi):
```json
{ "id": 42, "status": "Queued" }
```

`requestedByUsername` verilirse servis, PersonnelDirectory'den kullanicinin takimini
(`teamName`) otomatik cekip kayda ekler - admin panelindeki loglar bu alana gore de
filtrelenebilir. Alan opsiyoneldir; PersonnelDirectory kapaliysa ya da kullanici
bulunamazsa gonderim yine de kuyruklanir, sadece takim bilgisi bos kalir.

Ekli dosya gondermek icin `attachments: [{ "fileName": "rapor.pdf", "contentType":
"application/pdf", "contentBase64": "..." }]` alanini ekleyin.

### Durum sorgula

```bash
curl http://<host>:5401/api/mail/42/status -H "X-Api-Key: <anahtar>"
```

## Admin API (panel tarafindan kullanilir, `X-Admin-Key` gerektirir)

- `GET/PUT /api/admin/relay-settings` - relay hesabi ayarlari (parola sadece PUT'ta, bos
  birakilirsa mevcut deger korunur; GET yanitinda asla acik parola donmez).
- `GET /api/admin/mail-logs?search=&status=&username=&team=&from=&to=&page=&pageSize=` -
  sayfalanmis arama/rapor.
- `GET /api/admin/mail-logs/{id}` - tek kaydin tum detayi (govde, ekler dahil).
- `GET/POST /api/admin/client-applications`, `PUT .../{id}/enabled` - istemci uygulama
  (API anahtari) yonetimi.
- `GET /api/admin/teams` - PersonnelDirectory'den senkronize edilen takim listesi (rapor
  filtresi icin).

## Onemli notlar

- `Queue:WorkerCount` ve DB'deki `RelaySettings.MaxConcurrentSend` degerlerinin kucuk olani,
  servis **basladiginda** efektif paralel gonderim sayisini belirler; `MaxConcurrentSend`'i
  admin panelinden degistirmek bir sonraki servis yeniden baslatilmasinda etkin olur.
- Basarisiz gonderimler ussel geri cekilme (exponential backoff, `Queue:BaseRetryDelaySeconds`
  taban, `Queue:MaxRetryDelaySeconds` tavan) ile tekrar denenir; `MaxAttempts`'e ulasinca
  `Failed` durumuna gecer ve tekrar denenmez (admin panelinden hata mesaji goruntulenebilir).
- Admin API anahtari basit, paylasimli bir anahtardir - servisin internal ag/ters proxy
  arkasinda calistirilmasi ve HTTPS ile sunulmasi onerilir. Kurumsal AD/JWT kimlik dogrulama
  ile degistirilmek istenirse `Security/AdminApiKeyFilter.cs` tek degisecek nokta.

## Git komutlari (bu dali cekmek icin)

```bash
git fetch origin claude/mail-sending-service-94wi9i
git checkout claude/mail-sending-service-94wi9i
# ya da mevcut bir klonda:
git pull origin claude/mail-sending-service-94wi9i
```
