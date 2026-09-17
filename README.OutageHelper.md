# OutageHelper (Kesinti Analiz Uygulamasi)

BT kesinti raporlarini okuyup siniflandiran ve planlanan degisikliklerin risk seviyesini
gecmis kesintilere dayanarak degerlendiren .NET 8 konsol uygulamalari seti.

## Cozum yapisi

- **Kesinti.Core** – modeller, SQL Server erisimi (Dapper), LiteLLM (OpenAI uyumlu) istemcisi.
- **Kesinti.Ingest** – 1. asama: `.docx` kesinti raporlarini okur, kural tabanli alan cikarir.
- **Kesinti.Classify** – 2. asama: LiteLLM ile kategori atar, standart disi dokumanlarda eksik alanlari doldurur.
- **Kesinti.Risk** – 3. asama: haftalik degisiklik listesini gecmis kesintilerle karsilastirip risk yorumu uretir.

## Kurulum

1. SQL Server'da veritabanini olusturup `sql/kesinti_schema.sql` scriptini calistirin.
2. Her projenin yaninda `appsettings.json` sablonu vardir; gercek degerleri commit etmeyin.
   Ortama ozel degerleri `appsettings.Production.json` (gitignore'da) veya ortam degiskenleri ile saglayin:

   ```
   Database__ConnectionString=Server=...;Database=...;...
   LiteLlm__BaseUrl=https://litellm.kurumsal.ornek/v1
   LiteLlm__ApiKey=***
   LiteLlm__ChatModel=gpt-4o-mini        # LiteLLM'de tanimli model adi
   LiteLlm__EmbeddingModel=text-embedding-3-small
   ```

   LiteLLM URL ve anahtari asla koda yazilmaz; her zaman appsettings/env'den okunur.

3. `dotnet build OutageHelper.sln`

## Calistirma

```bash
# 1. asama: klasordeki .docx dosyalarini oku
dotnet run --project src/Kesinti.Ingest -- /yol/kesinti-raporlari-klasoru

# 2. asama: bekleyen kayitlari LiteLLM ile siniflandir
dotnet run --project src/Kesinti.Classify

# 2. asama - elle duzeltme (kategori bilgisi hatali oneriliyorsa)
dotnet run --project src/Kesinti.Classify -- duzelt <kesintiSiniflandirmaId> "<Yeni Kategori Adi>" <kullaniciAdi>

# 3. asama: haftalik degisiklik listesini degerlendir
dotnet run --project src/Kesinti.Risk -- /yol/degisiklik-listesi.csv
```

## Ornekler

`samples/` klasorunde yalnizca anonim ornekler bulunur (gercek kesinti dokumanlari repoya
konulmaz):

- `ornek_kesinti_standart.docx` – standart formatta bir kesinti raporu ornegi.
- `ornek_kesinti_standart_disi.docx` – standart disi (serbest metin) bir rapor ornegi;
  eksik alanlar 2. asamada AI tarafindan doldurulur.
- `degisiklik_listesi_ornek.csv` – 3. asama icin haftalik degisiklik listesi ornegi.

## Onemli kurallar

- Ham metin her zaman saklanir; standart formata uymayan dokumanlar
  `FormatDurumu = 'Standart disi'` olarak isaretlenir, silinmez.
- Siniflandirma asamasinda kategori her zaman kapali `Kategori` listesinden secilir;
  modelin listede olmayan bir kategori uretmesi hata olarak islenir.
- Risk asamasinda LLM, kendisine sunulmayan bir kesinti kaydina asla atifta bulunamaz;
  ayni sistemde gecmis kayit veya yeterli benzerlik yoksa risk seviyesi `Belirsiz` olarak
  isaretlenir ve model cagrilmaz (uydurma onlenir).
