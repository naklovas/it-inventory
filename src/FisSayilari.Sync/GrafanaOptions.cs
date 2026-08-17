namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // Sadece "Referer" header'ini olusturmak icin kullaniliyor - calisan tarayici istegiyle
    // birebir ayni gorunmesi icin.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1&viewPanel=24";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile istek atilir.
    public string ApiToken { get; set; } = "";

    // Gecici cozum: tarayicidan (F12 > Network > istegin "Cookie" header'i, ya da
    // curl komutundaki -b "..." icerigi) kopyalanan TAM cerez metni, oldugu gibi -
    // ornek: "grafana_session=xxx; grafana_session_expiry=yyy". Sadece grafana_session
    // gonderip grafana_session_expiry'i atlamak 401'e yol aciyor, ikisi birlikte gerekli.
    // Sadece hizli test icin - bir sure sonra suresi dolar. ApiToken bosken, SessionCookie
    // doluysa bu kullanilir.
    public string SessionCookie { get; set; } = "";
}
