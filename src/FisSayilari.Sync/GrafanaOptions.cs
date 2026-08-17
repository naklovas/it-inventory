namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // Grafana, sayfa ilk yuklendiginde (SSO/Windows auth ile) bir oturum cerezi veriyor;
    // API cagrilari bu cerez olmadan 401 donebiliyor. Bu yuzden once bu sayfaya
    // bir "isinma" istegi atip cerezi aliyoruz, sonra ayni HttpClient ile proxy'yi cagiriyoruz.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile istek atilir, cerez/SSO hic devreye girmez.
    public string ApiToken { get; set; } = "";

    // Gecici cozum: tarayicidan (F12 > Network) kopyalanan grafana_session cerez degeri.
    // Sadece test icin - bir sure sonra suresi dolar (grafana_session_expiry), kalici script'te
    // ApiToken kullanin. ApiToken bosken, SessionCookie doluysa bu kullanilir.
    public string SessionCookie { get; set; } = "";
}
