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

   **Onemli - sunucudaki gercek degerleri dogrudan bu dosyada degistirmeyin:**
   repodaki `appsettings.json` sadece genel/varsayilan degerleri tasir; her kod guncellemesi
   (`git pull` + yeniden `dotnet publish`) bu dosyanin uzerine yazar ve sunucuya girdiginiz
   gercek connection string/admin key/relay bilgileri kaybolur. Bunun yerine, **yayinlanan
   klasorde** (sadece o sunucuda, repoya hic girmez) `appsettings.Production.json` adinda
   ayrica bir dosya olusturun ve sadece degistirmek istediginiz alanlari icine yazin:
   ```json
   {
     "ConnectionStrings": { "MailDb": "Server=...;Database=...;..." },
     "Admin": { "ApiKey": "<guclu-bir-deger>" }
   }
   ```
   ASP.NET Core, Windows Servisi/production olarak calisirken (`ASPNETCORE_ENVIRONMENT`
   ayarlanmamissa varsayilan ortam "Production"dir) bu dosyayi appsettings.json'un **uzerine**
   otomatik olarak katmanlar - kod tarafinda hicbir degisiklik gerekmez. Dosya repoda
   olmadigindan (`.gitignore`'a eklendi) hicbir `dotnet publish` bunu silmez/degistirmez,
   siz elle guncellemedikce oldugu gibi kalir. Alternatif olarak ortam degiskeni de
   kullanilabilir (orn. `setx ConnectionStrings__MailDb "..."`) - ikisi de aynı sekilde
   appsettings.json'daki degerin onune gecer.
3. **Calistirma (gelistirme/test):**
   ```bash
   cd src/MailRelay.Service
   dotnet run
   ```
4. **Ilk admin girisi:** Tarayicidan `http://<host>:5401/admin/` adresine gidip yukaridaki
   `Admin:ApiKey` degerini panelin sag ust kosesindeki alana girin (sadece bu sekmenin
   sessionStorage'inda tutulur). "Relay Ayarları" sekmesinden gercek SMTP relay bilgilerini
   girip kaydedin (appsettings.json'daki yer tutucu degerlerin uzerine yazilir).
5. **Istemci uygulama tanimlama:** Admin panelinde "Uygulamalar" sekmesinden her cagiran
   uygulama icin bir kayit olusturun (panel anahtari kendisi uretir); uretilen API
   anahtarini o uygulamanin konfigurasyonuna (guvenli sekilde) kaydedin. Anahtar sadece
   olusturma aninda degil, "Uygulamalar" listesinde her zaman goruntulenebilir.

## Windows Servisi olarak yayinlama (production)

Bu servis surekli calisan bir arka plan servisi oldugundan (kuyruk worker'lari, SMTP
gonderimi) production'da konsol uygulamasi olarak degil, **Windows Servisi** olarak
kurulmasi onerilir - sunucu yeniden baslasa bile otomatik ayaga kalkar. Proje zaten
`Microsoft.Extensions.Hosting.WindowsServices` paketiyle bu moda hazir
(`builder.Services.AddWindowsService(...)` - `dotnet run` ile konsoldan calistirinca hicbir
etkisi yok, sadece `sc create` ile servis olarak kurulunca devreye girer).

Proje **net10.0** hedefler (guncel .NET LTS surumu). Sunucuya `dotnet-hosting-10.0.x`
(ASP.NET Core Hosting Bundle) kurulduysa asagidaki **framework-dependent** yayin
yeterlidir - daha kucuk cikti, sunucudaki ortak runtime'i kullanir:

```powershell
# 1) Yayinla (framework-dependent - sunucuda .NET 10 Hosting Bundle kurulu olmali)
dotnet publish -c Release -r win-x64 --self-contained false -o C:\Servisler\MailRelay

# 2) Windows Servisi olarak kaydet (Yonetici PowerShell)
sc.exe create MailRelayService binPath= "C:\Servisler\MailRelay\MailRelay.Service.exe" start= auto
sc.exe description MailRelayService "Uygulamalar icin kuyruklu mail gonderme servisi"
sc.exe start MailRelayService

# Durdurmak/kaldirmak icin:
sc.exe stop MailRelayService
sc.exe delete MailRelayService
```

Hosting Bundle'i kurmak istemediginiz/kuramadiginiz bir sunucu icin alternatif olarak
**self-contained** yayin da kullanilabilir - .NET runtime'i uygulamanin kendi klasorune
gomer, sunucudaki ortak kuruluma hic dokunmaz, hicbir onkosul gerektirmez (sadece dosya
boyutu daha buyuktur):

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -o C:\Servisler\MailRelay
```

(2. adimdaki `sc.exe` komutlari her iki yayin turunde de aynidir.)

Gercek/gizli ayarlari (`ConnectionStrings:MailDb`, `Admin:ApiKey` vb.) `appsettings.json`
yerine yayinlanan klasordeki `appsettings.Production.json`'a yazin (yukaridaki "Onemli"
notuna bakin) - boylece bir sonraki `dotnet publish`'te kaybolmaz. Servis her yeniden
baslatilmasinda ikisini de (once appsettings.json, sonra uzerine appsettings.Production.json)
okur.

### Portlar otomatik acilir mi?

`Kestrel:Endpoints` altinda tanimladigimiz 3 port (5401/5402/5403), servis (ister konsoldan
ister Windows Servisi olarak) **baslar baslamaz kendisi dinlemeye alir** - ayrica bir kod/
komut gerekmez, appsettings.json'da tanimli olmalari yeterli. Ama iki nokta dikkat ister:

- **Windows Firewall:** Kestrel'in bir portu dinlemeye baslamasi, o portu **baska
  makinelerden gelen** baglantilara otomatik acmaz - Windows Firewall inbound kurali
  ayrica eklenmelidir (sadece localhost'tan/aynimakineden cagiriliyorsa gerek yoktur):
  ```powershell
  New-NetFirewallRule -DisplayName "MailRelay 5401-5403" -Direction Inbound `
    -LocalPort 5401-5403 -Protocol TCP -Action Allow
  ```
- **Port cakismasi:** 5401-5403 baska bir uygulama tarafindan kullaniliyorsa
  appsettings.json > `Kestrel:Endpoints` altindaki degerleri degistirip servisi yeniden
  baslatmaniz yeterli - kac port/hangi numaralar oldugu tamamen bu ayardan gelir.

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

`fromDisplayName` verilirse **sadece gorunen ad** o mail icin degistirilir (orn. "IK
Sistemi"); gonderen e-posta ADRESI her zaman admin panelindeki tek relay hesabindan
gelir - istemci bunu degistiremez. Alan opsiyoneldir, bos birakilirsa merkezi
`RelaySettings.FromDisplayName` kullanilir.

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
