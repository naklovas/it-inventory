namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // InfluxDB datasource'unun proxy sorgusunda bekledigi "db" query parametresi.
    public string InfluxDbName { get; set; } = "test";

    // Kalici/dogru cozum: Grafana'da olusturulan bir Service Account Token.
    // Doluysa "Authorization: Bearer <ApiToken>" ile istek atilir.
    public string ApiToken { get; set; } = "";

    // Gecici cozum: tarayicidan (F12 > Network) kopyalanan grafana_session cerez degeri.
    // Sadece hizli test icin - bir sure sonra suresi dolar (grafana_session_expiry).
    // ApiToken bosken, SessionCookie doluysa bu kullanilir.
    public string SessionCookie { get; set; } = "";
}
