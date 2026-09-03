namespace MailRelay.Service.Options;

// appsettings.json > Admin. Basit paylasimli anahtar tabanli koruma; internal ag/ters proxy
// arkasinda kullanim icin yeterlidir. Kurumsal AD/JWT kimlik dogrulamasi ile degistirilebilir.
public sealed class AdminOptions
{
    public string ApiKey { get; set; } = "";
}
