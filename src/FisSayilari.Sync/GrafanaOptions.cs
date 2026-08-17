namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // Oturumu kurmak icin once tarayicida acilan dashboard sayfasi.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile duz HTTP istegi atilir, tarayici hic acilmaz.
    public string ApiToken { get; set; } = "";

    // ApiToken bos oldugunda kullanilan yol: Playwright ile gercek bir Edge penceresi acilip
    // (playwright-profile/ klasorunde saklanan kalici profille) dashboard sayfasina gidilir,
    // boylece o tarayicinin SSO/Windows oturumu kullanilir. Ilk calistirmada SSO otomatik
    // tamamlanmazsa Headless=false ile pencereyi gorup elle giris yapabilirsiniz; sonraki
    // calistirmalarda ayni profil sayesinde Headless=true yeterli olur.
    public bool Headless { get; set; } = true;
}
