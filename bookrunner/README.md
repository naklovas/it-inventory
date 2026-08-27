# BookRunner

Buyuk altyapi gecislerinde ve altyapi + kod yayginlastirmanin birlikte yurudugu
calismalarda kullanilan **runbook hazirlama ve isbirligi platformu**.

Amac: yapilacak isi bir baslik altinda tarif etmek, adimlari (task) tanimlamak,
adimlari Active Directory'deki kisilere ve gruplara atamak, calisma sirasinda
herkesin ayni tabloyu gormesini saglamak ve her hareketin izini birakmak.

---

## Icindekiler

- [Ozellikler](#ozellikler)
- [Mimari](#mimari)
- [Cozum yapisi](#cozum-yapisi)
- [Yetkilendirme modeli](#yetkilendirme-modeli)
- [Kurulum](#kurulum)
- [Yapilandirma](#yapilandirma)
- [Calistirma ve dagitim](#calistirma-ve-dagitim)
- [API](#api)
- [Varsayimlar](#varsayimlar)

---

## Ozellikler

| Alan | Karsiligi |
| --- | --- |
| Kimlik dogrulama | Windows Entegre Kimlik Dogrulama (Negotiate/Kerberos) - SSO, ayrica oturum acilmaz |
| Kullanici yonetimi | Tamamen Active Directory: kisi, grup, uyelik, unvan, departman ve **fotograf** AD'den okunur |
| Kisi rozetleri | Goreve atanan kisilerde AD **thumbnail'i + adin bas harfleri** birlikte; fotograf yoksa bas harf rozeti. Sag ust kosede bagli kullanicinin fotografi ve tam adi |
| Runbook | Baslik + aciklama + planlanan zaman + etiketler + SCSM kaydi |
| Gorev (task) | Sirali adimlar, her adim **kendi renginde bir bar**, oncelik, sure, bagimlilik, geri alma notu |
| Atama | Kisiye veya dogrudan **AD grubuna**; gruptaki kisilere e-posta gider |
| Devir (handover) | Goreve atanan kisi gorevi baska kisiye/gruba devredebilir, devir zinciri saklanir |
| Yorum | Her gorevin altinda yorum akisi, @ ile kisi anma, anilan kisilere e-posta |
| Tarihce | Goreve tiklaninca **akordiyon** icinde acilan degismez olay gecmisi |
| Sablon | Runbook sablona cevrilir, sablondan yeni runbook uretilir |
| Listeleme/filtreleme | Arama, durum, etiket, SCSM kaydi, tarih araligi, siralama, sayfalama |
| Excel | Runbook ve liste disa aktarimi + sablonla gorev ice aktarimi (dogrulamali) |
| PDF | Yazdirmaya uygun runbook ciktisi (gorev barlari, atananlar, yorumlar) |
| E-posta | Atama, devir, yorum ve durum degisikliginde bildirim (outbox + tekrar deneme) |
| Audit trail | Varlik degisiklikleri otomatik, disa aktarim/script gibi islemler acikca kaydedilir |
| Service Manager | SCSM veritabanina **salt-okunur**, dogrudan SQL erisimi |
| 3. parti API | Runbook/gorev olaylarinin dis bir REST ucuna iletilmesi (webhook/ITSM/sohbet kanali) |
| Otomasyon | Roslyn C# Scripting (CSX) ile gorevlere baglanan script'ler |
| Canli isbirligi | SignalR: gorev/yorum degisiklikleri acik ekranlara aninda yansir |

---

## Mimari

Katmanli (N-tier), **ayri REST API + ayri frontend**:

```
┌──────────────────┐        HTTPS + Negotiate        ┌──────────────────┐
│  BookRunner.Web  │ ──────────────────────────────► │  BookRunner.Api  │
│  (MVC / Razor)   │ ◄────────────────────────────── │  (REST + Swagger)│
│  Bootstrap 5     │        JSON + SignalR           │                  │
└──────────────────┘                                 └────────┬─────────┘
                                                              │
                                            ┌─────────────────┼─────────────────┐
                                            ▼                 ▼                 ▼
                                   ┌────────────────┐ ┌──────────────┐ ┌────────────────┐
                                   │ SQL Server     │ │ Active       │ │ Service        │
                                   │ (EF Core 9)    │ │ Directory    │ │ Manager DB     │
                                   │                │ │ (salt okuma) │ │ (salt okuma)   │
                                   └────────────────┘ └──────────────┘ └────────────────┘
```

Katmanlar ve bagimlilik yonu:

```
BookRunner.Web ─┐
                ├─► BookRunner.Application ─► BookRunner.Domain
BookRunner.Api ─┤              ▲
                └─► BookRunner.Infrastructure ─┘
```

- **Domain** - varliklar ve enum'lar. Hicbir seye bagimli degil.
- **Application** - is kurallari, DTO'lar, servis sozlesmeleri, izin modeli.
  Veri erisimini `IAppDbContext` soyutlamasi uzerinden yapar; SQL Server'i tanimaz.
- **Infrastructure** - EF Core/SQL Server, Active Directory, SMTP, Excel, PDF,
  Service Manager, Roslyn scripting, dis API entegrasyonu.
- **Api** - REST uclari, Swagger, Negotiate kimlik dogrulama, izin politikalari,
  SignalR hub'i.
- **Web** - MVC/Razor arayuz. Is mantigina dogrudan erisimi yoktur; tum veriyi
  REST uzerinden alir.

### Tarayici neden API'ye dogrudan gitmiyor?

Ekran ici etkilesimler (gorev ekleme, atama, yorum, tarihce) tarayicidan
**Web katmanindaki JSON uclarina** gider, Web de istegi kullanicinin Windows
kimligiyle API'ye iletir. Bunun nedeni Kerberos ve CORS karmasikligini tek
noktada toplamak. API yine de dogrudan cagrilabilir (CORS politikasi
`Cors:AllowedOrigins` ile acik) - Swagger UI, PowerShell veya baska bir istemci
bu yolu kullanabilir.

---

## Cozum yapisi

```
bookrunner/
├── BookRunner.sln
├── Directory.Build.props          # ortak derleme ayarlari (net9.0, nullable, XML doc)
├── Directory.Packages.props       # merkezi paket surum yonetimi
├── publish.ps1                    # tek dosyalik Windows exe uretir
├── run-local.ps1                  # API + Web'i birlikte baslatir
├── sql/
│   ├── 01_CreateDatabase.sql      # veritabani, sema ve uygulama hesabi
│   ├── 02_BookRunner_Schema.sql   # EF Core migration'indan uretilen idempotent script
│   ├── 03_ServiceManager_ReadOnly.sql
│   └── 04_RoleMappings.sql        # AD grubu -> rol eslemesi
├── scripts/                       # ornek CSX script'leri
└── src/
    ├── BookRunner.Domain/
    ├── BookRunner.Application/
    ├── BookRunner.Infrastructure/
    ├── BookRunner.Api/
    └── BookRunner.Web/
```

---

## Yetkilendirme modeli

Uygulamada ayri bir kullanici/rol ekrani **yoktur**. Yetki iki kaynaktan gelir:

```
AD grubu ──(RoleMappings tablosu)──► AppRole ──► izin claim'leri
                                                      +
                            runbook sahipligi ──► kendi runbook'unda tam yetki
```

### Roller

| Rol | Ozet | Baslica izinler |
| --- | --- | --- |
| `Viewer` | Sadece okur | `runbook.read` |
| `Contributor` | Kendi gorevlerini yurutur, yorum yazar, devreder | `task.execute`, `task.comment`, `data.export` |
| `RunbookAuthor` | Runbook/gorev olusturur, kisi ve gruplara atar, sablon yayinlar | + `runbook.write`, `task.assign`, `data.import` |
| `Administrator` | **Tam yetki**: her runbook'ta silme dahil her sey | + `runbook.delete`, `task.delete`, `script.manage`, `audit.read`, `admin.manage` |

Bir kullanici birden fazla gruba uye ise **en yuksek rol** gecerlidir. Hicbir
eslemeye uymayan kullanici `Viewer` olur. Eslemeler `appsettings.json` icindeki
`Authorization:RoleMappings` bolumunden (ilk acilista tohumlanir) veya
`sql/04_RoleMappings.sql` ile yonetilir.

### Runbook sahipligi

**Runbook'u olusturan kisi onun sahibi olur ve kendi runbook'u uzerinde her
degisikligi yapabilir** - rol izni olmasa bile:

- runbook'u duzenleme, durumunu degistirme, **silme**
- gorev ekleme, duzenleme, siralama, **silme**
- kisi/grup atama, atama kaldirma, devretme
- yorum yazma, Excel'den gorev aktarma, sablona cevirme

Sahiplik `Runbooks.OwnerUserId` alanindan okunur ve runbook duzenleme ekranindan
baska bir kisiye devredilebilir.

### Kim neyi yapabilir - ozet

| Islem | Viewer | Contributor | RunbookAuthor | **Sahip** | **Administrator** |
| --- | :-: | :-: | :-: | :-: | :-: |
| Runbook goruntuleme | ✔ | ✔ | ✔ | ✔ | ✔ |
| Runbook olusturma | | | ✔ | - | ✔ |
| Runbook duzenleme | | | ✔ | ✔ | ✔ |
| **Runbook silme** | | | | ✔ | ✔ |
| Gorev ekleme/duzenleme | | | ✔ | ✔ | ✔ |
| **Gorev silme** | | | | ✔ | ✔ |
| **Atama ekleme/kaldirma** (kisi veya grup) | | | ✔ | ✔ | ✔ |
| Kendi gorevini ilerletme/devretme | | ✔ | ✔ | ✔ | ✔ |
| Yorum yazma | | ✔ | ✔ | ✔ | ✔ |
| Sablon yayinlama | | | ✔ | ✔ | ✔ |
| Script yazma | | | | | ✔ |
| Denetim kayitlari | | | | | ✔ |

> "Sahip" sutunu yalnizca **kendi** runbook'u icin gecerlidir; Administrator ayni
> yetkilere **tum** runbook'larda sahiptir. Runbook olusturmak icin `runbook.write`
> gerekir (henuz bir sahiplik olusmadigi icin).

Yetki karari tek yerde, is katmanindaki `IRunbookAccess` icinde verilir. API
uclarindaki `[Authorize]` politikalari yalnizca taban erisimi (`runbook.read`)
kontrol eder; asil "izin mi, sahiplik mi" karari servis katmanindadir.
Kullanici profili ve grup uyelikleri her istekte AD'ye gidilmeden, 15 dakikalik
bellek onbelleginden okunur.

### Atama: kisi ve grup birlikte

Bir goreve hem **kisi** hem **AD grubu** atanabilir, ayni gorevde birden fazla
atama bulunabilir. Kisi atamalarinda AD'deki fotograf (thumbnail) ile adin bas
harfleri yan yana gosterilir; fotografi olmayan kisilerde yalnizca bas harf
rozeti cikar. Fotograf yuklenemezse rozet kendiliginden bas harflere doner. Grup atamasinda bildirim, grubun kendi e-posta adresine
(varsa) ya da AD'deki uyelerinin adreslerine gider. Goreve atanan kisi gorevi
baska bir kisiye veya gruba devredebilir; devir zinciri tarihcede saklanir.

---

## Kurulum

### Gereksinimler

- .NET 9 SDK (gelistirme) / Windows Server (calisma)
- SQL Server 2019+
- Etki alanina uye bir sunucu (Windows kimlik dogrulama icin)
- SMTP sunucusu (bildirimler icin)

### Adimlar

> Bu klasor tek dosyalik bir kurulum paketinden de uretilebilir:
> depo kokundeki `scaffold-bookrunner.ps1` cozumun tamamini (kaynak kod, SQL
> script'leri, arayuz kutuphaneleri) icinde tasir ve `.\scaffold-bookrunner.ps1`
> komutuyla diske acip derler. Internet baglantisi gerekmez.
> Kaynaklar degistikten sonra paketi `tools\New-BookRunnerScaffold.ps1` ile
> yeniden uretin.

```powershell
# 1. Veritabani
sqlcmd -S localhost -i sql\01_CreateDatabase.sql
sqlcmd -S localhost -d BookRunner -i sql\02_BookRunner_Schema.sql

# 2. AD grup -> rol eslemesi (SID degerlerini kendi gruplarinizla degistirin)
#    Grup SID'ini ogrenmek icin:  (Get-ADGroup 'BookRunner-Authors').SID.Value
sqlcmd -S localhost -d BookRunner -i sql\04_RoleMappings.sql

# 3. (opsiyonel) Service Manager salt-okunur erisimi
sqlcmd -S scsm-dw -i sql\03_ServiceManager_ReadOnly.sql

# 4. Calistir
.\run-local.ps1
```

> `Database:MigrateOnStartup` varsayilan olarak `true`'dur; API acilista bekleyen
> EF Core migration'larini kendisi uygular. Sema degisikliklerini elle yonetmek
> isterseniz bu ayari `false` yapin ve yalnizca `sqlcmd` ile ilerleyin.

Sema degisikligi uretmek icin:

```powershell
dotnet ef migrations add <Ad> `
    --project src/BookRunner.Infrastructure `
    --startup-project src/BookRunner.Api `
    --output-dir Persistence/Migrations
```

---

## Yapilandirma

Tum ayarlar `src/BookRunner.Api/appsettings.json` ve
`src/BookRunner.Web/appsettings.json` dosyalarindadir.

### API

Tum SQL Server baglanti dizeleri tek bir `ConnectionStrings` bolumunde toplanir:

```jsonc
"ConnectionStrings": {
  "BookRunner":     "Server=SQLSRV01;Database=BookRunner;Integrated Security=true;...",
  "ServiceManager": "Server=SCSMDW01;Database=DWDataMart;Integrated Security=true;...ApplicationIntent=ReadOnly"
}
```

Active Directory etki alanlari da tamamen appsettings'ten yonetilir. Tek
domainli kurulumda `Domain` yeterlidir; birden fazla etki alani (orman ici alt
domainler veya guven iliskisi olan domainler) varsa `Domains` listesi doldurulur:

```jsonc
"ActiveDirectory": {
  "Domain": "contoso.com",
  "Domains": [
    { "Name": "contoso.com",      "NetBiosName": "CONTOSO", "SearchRoot": "DC=contoso,DC=com" },
    { "Name": "emea.contoso.com", "NetBiosName": "EMEA",    "SearchRoot": "DC=emea,DC=contoso,DC=com" }
  ]
}
```

Arama sorgulari tum etki alanlarinda calisir ve sonuclar birlestirilir; SID
cozumlemeleri ilk eslesmede durur. Kullanici `CONTOSO\ali` seklinde oturum
actiginda `NetBiosName` esleşen etki alanina once sorulur. Bir etki alanina
erisilemezse digerleri calismaya devam eder.

| Bolum | Anahtar | Aciklama |
| --- | --- | --- |
| `ConnectionStrings` | `BookRunner` | Uygulama veritabani |
| | `ServiceManager` | SCSM Data Warehouse (salt-okunur) |
| `Database` | `MigrateOnStartup` | Acilista migration uygula |
| `Cors` | `AllowedOrigins` | Frontend adresleri |
| `ActiveDirectory` | `Domain`, `SearchRoot` | Tek domainli kurulum |
| | `Domains[]` | Cok domainli kurulum (`Name`, `NetBiosName`, `SearchRoot`, servis hesabi) |
| | `ServiceAccountUserName/Password` | Bos ise uygulama kimligiyle baglanilir (onerilen) |
| | `PhotoAttributes` | Fotograf niteligi sirasi (`thumbnailPhoto`, `jpegPhoto`) |
| | `Disabled` | AD'siz gelistirme ortami icin |
| `Authorization` | `RoleMappings` | AD grubu -> rol tohumlamasi |
| `Email` | `Host`, `Port`, `UseStartTls`, `FromAddress` | SMTP |
| | `WebBaseUrl` | E-postalardaki baglantilarin taban adresi |
| | `RedirectAllTo` | Doluysa tum posta bu adrese gider (test ortami) |
| `ServiceManager` | `Enabled` | SCSM entegrasyonunu ac/kapat |
| | `SearchQuery`, `GetByIdQuery` | SCSM sorgulari **yapilandirmadan** degistirilebilir |
| `Scripting` | `Enabled`, `BlockedPatterns` | CSX calistirici |
| `Integration` | `Enabled`, `BaseUrl`, `ApiKey` | 3. parti REST entegrasyonu |

`appsettings.json` yorum (`//`, `/* */`) destekler; ornek dosyada her bolum
aciklamalidir.

### Web

| Anahtar | Aciklama |
| --- | --- |
| `Api:BaseUrl` | API adresi |
| `Api:HubUrl` | Bos ise `BaseUrl/hubs/runbook` olarak turetilir |

Sifre gibi degerleri dosyaya yazmak yerine ortam degiskeni veya
`dotnet user-secrets` kullanin:

```powershell
setx Email__Password "..." /M
setx ConnectionStrings__BookRunner "Server=...;Integrated Security=true" /M
```

---

## Calistirma ve dagitim

### Gelistirme

```powershell
.\run-local.ps1
# Web    : https://localhost:7080
# API    : https://localhost:7443
# Swagger: https://localhost:7443/swagger
```

### Local exe (self-contained tek dosya)

```powershell
.\publish.ps1 -OutputPath C:\BookRunner
```

Cikan iki klasor hedef sunucuya kopyalanip dogrudan calistirilabilir; makinede
.NET kurulu olmasi gerekmez. Windows servisi olarak kaydetmek icin:

```powershell
sc.exe create BookRunnerApi binPath= "C:\BookRunner\BookRunner.Api\BookRunner.Api.exe" obj= "CONTOSO\svc-bookrunner" start= auto
sc.exe create BookRunnerWeb binPath= "C:\BookRunner\BookRunner.Web\BookRunner.Web.exe" obj= "CONTOSO\svc-bookrunner" start= auto
```

Her iki uygulama `UseWindowsService()` ile yapilandirildigi icin hem konsoldan
hem de servis olarak calisir.

### Kerberos notu

Web ve API **ayni sunucuda** calisir; bu kurulumda ek yapilandirma gerekmez.
Web, kullanicinin Windows kimligini API'ye kendi sureci uzerinden tasir.

Ileride iki uygulamayi ayri sunuculara ayirmak isterseniz Kerberos kisitlanmis
yetki devri (constrained delegation) gerekir:

```powershell
setspn -S HTTP/bookrunner-api.contoso.com CONTOSO\svc-bookrunner
Set-ADUser svc-bookrunner-web -Add @{
    'msDS-AllowedToDelegateTo' = 'HTTP/bookrunner-api.contoso.com'
}
```

---

## API

Swagger UI: `https://<api-host>/swagger`

| Grup | Uc |
| --- | --- |
| Runbook | `GET/POST /api/runbooks`, `GET/PUT/DELETE /api/runbooks/{id}`, `GET /api/runbooks/dashboard` |
| Sablon | `POST /api/runbooks/{id}/save-as-template`, `POST /api/runbooks/templates/{id}/instantiate` |
| Gorev | `POST /api/runbooks/{id}/tasks`, `PUT /api/tasks/{id}`, `POST /api/tasks/{id}/status`, `POST /api/runbooks/{id}/tasks/reorder` |
| Tarihce | `GET /api/tasks/{id}/history` |
| Atama | `GET/POST /api/tasks/{id}/assignments`, `POST /api/tasks/{id}/assignments/handover` |
| Yorum | `GET/POST /api/tasks/{id}/comments` |
| Dizin | `GET /api/directory/me`, `/users`, `/groups`, `/users/{id}/photo` |
| Disa aktarim | `GET /api/runbooks/{id}/export/excel`, `/export/pdf`, `POST /api/runbooks/{id}/import/excel` |
| Denetim | `GET /api/audit` |
| Service Manager | `GET /api/service-manager/work-items`, `/health` |
| Script | `GET/POST /api/scripts`, `POST /api/scripts/{id}/run` |
| SignalR | `/hubs/runbook` |

Hatalar RFC 7807 `ProblemDetails` olarak doner:
`404` bulunamadi, `403` yetkisiz, `400` dogrulama, `409` is kurali ihlali.

---

## Varsayimlar

Gereksinimlerde acikca belirtilmeyen, tasarim sirasinda alinan kararlar:

1. **AD salt-okunur.** Uygulama Active Directory'ye hicbir sey yazmaz. Kisi,
   grup, uyelik ve fotograf bilgisi AD'den okunup yerel tablolara yansitilir
   (12 saatlik tazeleme). Boylece raporlar hizli calisir ve AD'ye gecici olarak
   erisilemedigi anlarda uygulama okunabilir kalir.

2. **Yetki AD grubundan + runbook sahipliginden gelir.** Ayri bir kullanici/rol
   yonetimi ekrani yapilmadi; roller `RoleMappings` tablosundaki AD grubu
   eslemelerinden turetiliyor. Hicbir eslemeye uymayan kullanici `Viewer` olur.
   Buna ek olarak runbook'un sahibi, rol izni olmasa da kendi runbook'unda her
   degisikligi yapabilir. Silme islemleri (`runbook.delete`, `task.delete`)
   yalnizca `Administrator` rolunde ve runbook sahibindedir.

3. **Sablon ile runbook ayni varliktir.** `IsTemplate` bayragi ile ayrilir.
   Bu, "runbook template hale getirilebilir" gereksinimini kod ve sema
   tekrarlamadan karsilar.

4. **Silme mantiksaldir.** Runbook, gorev ve yorumlar `IsDeleted` ile
   isaretlenir; denetim izleri ve gecmis atamalar kaybolmaz.

5. **E-posta once kuyruga yazilir.** SMTP arizasi kullanici islemini bozmaz;
   arka plan servisi ustel bekleme ile tekrar dener. Hangi bildirimin kime
   gittigi `EmailOutbox` tablosundan denetlenebilir.

6. **Grup atamasinda alicilar.** Grubun kendi e-posta adresi varsa dagitim
   listesi olarak kullanilir; yoksa AD'deki (ic ice gruplar dahil) uyelerin
   adresleri cozulur.

7. **Service Manager sorgulari yapilandirilabilir.** Varsayilan sorgular SCSM
   Data Warehouse'daki `ChangeRequestDimvw` gorunumune gore yazildi. Ortamdaki
   sema/ozellestirme farkliysa `ServiceManager:SearchQuery` ve `GetByIdQuery`
   ayarlarindan degistirilir - kod degistirmek gerekmez. Erisim yalnizca
   `SELECT`; `03_ServiceManager_ReadOnly.sql` yazma yetkisini acikca reddeder.

8. **CSX script'leri tam guvenle calisir.** Roslyn scripting bir guvenlik
   siniri saglamaz. Bu yuzden script **yazma** yetkisi yalnizca
   `Administrator` rolunde, her calistirma audit'e yaziliyor,
   `Scripting:BlockedPatterns` listesindeki ifadeler reddediliyor ve zaman
   asimi uygulaniyor. Guvenilmeyen kullanicilara `script.manage` izni vermeyin.

9. **Frontend, API sozlesmelerini yeniden kullanir.** `BookRunner.Web` projesi
   DTO'lar icin `BookRunner.Application`'a referans verir; is mantigina
   dogrudan erisimi yoktur, tum veri REST uzerinden gelir. Ayri bir
   `Contracts` projesi tercih edilirse DTO'lar oraya tasinabilir.

10. **Tarayici API'ye dogrudan gitmez.** Ekran ici etkilesimler Web katmanindaki
    JSON uclari uzerinden vekillenir (Kerberos/CORS karmasikligini tek noktada
    tutmak icin). API dogrudan cagrilara da aciktir. Web ve API ayni sunucuda
    calistigi icin yetki devri (delegation) yapilandirmasina gerek yoktur.

11. **Cok domainli AD destegi eklendi.** Tek domainli kurulum icin ek ayar
    gerekmiyor; `ActiveDirectory:Domains` bos birakildiginda kok ayarlardan tek
    etki alani turetilir. Birden fazla etki alani tanimlanirsa aramalar hepsinde
    calisir. Bir etki alanina erisilemezse hata loglanir ve digerleri devam eder.

12. **Frontend kutuphaneleri yerelde.** Bootstrap 5, Bootstrap Icons ve SignalR
    istemcisi `wwwroot/lib` altinda gomulu; internet erisimi olmayan ic aglarda
    da arayuz eksiksiz calisir.

13. **Test yok.** Gereksinimde belirtildigi gibi otomatik test projesi
    eklenmedi. Is kurallari `Application` katmaninda toplandigi icin sonradan
    test eklemek icin mimari degisiklik gerekmez.

14. **Turkce metinler ASCII ile yazildi.** Farkli kod sayfalari ve konsol
    ortamlarinda bozulma yasanmamasi icin arayuz metinlerinde ve yorumlarda
    Turkce karakter kullanilmadi. Tam Turkce metin istenirse kaynak dosyalar
    UTF-8 oldugu icin dogrudan degistirilebilir.
