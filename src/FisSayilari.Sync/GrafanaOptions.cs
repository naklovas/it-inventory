namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";

    // Anonim erisimde Grafana, sayfa ilk yuklendiginde bir oturum cerezi veriyor;
    // API cagrilari bu cerez olmadan 401 donebiliyor. Bu yuzden once bu sayfaya
    // bir "isinma" istegi atip cerezi aliyoruz, sonra ayni HttpClient ile proxy'yi cagiriyoruz.
    public string DashboardPath { get; set; } = "/d/UN0bbgwnz/ziraat-bankasi-kanal-fis-sayilari?orgId=1";
}
