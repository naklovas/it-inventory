namespace FisSayilari.Sync;

public sealed class GrafanaOptions
{
    public string BaseUrl { get; set; } = "";
    public string DatasourceUid { get; set; } = "";
    public string Timezone { get; set; } = "Europe/Istanbul";
}
