using MailRelay.Service.Data;
using MailRelay.Service.Endpoints;
using MailRelay.Service.Options;
using MailRelay.Service.PersonnelDirectory;
using MailRelay.Service.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Windows'ta "sc create" ile Windows Servisi olarak kurulup calistirildiginda servis yasam
// dongusune (baslat/durdur, Event Log) duzgun entegre olur. "dotnet run" ile normal konsol
// uygulamasi olarak calistirildiginda (Linux dahil) hicbir etkisi yoktur - guvenle her ortamda kalabilir.
builder.Services.AddWindowsService(options => options.ServiceName = "MailRelayService");

builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("SmtpSettings"));
builder.Services.Configure<PersonnelDirectoryOptions>(builder.Configuration.GetSection("PersonnelDirectory"));
builder.Services.Configure<QueueOptions>(builder.Configuration.GetSection("Queue"));
builder.Services.Configure<AdminOptions>(builder.Configuration.GetSection("Admin"));

builder.Services.AddSingleton<SqlConnectionFactory>();
builder.Services.AddSingleton<MailQueueRepository>();
builder.Services.AddSingleton<RelaySettingsRepository>();
builder.Services.AddSingleton<ClientApplicationRepository>();

builder.Services.AddSingleton<MailQueueChannel>();
builder.Services.AddSingleton<RelaySettingsCache>();
builder.Services.AddSingleton<ISmtpMailSender, SmtpMailSender>();
builder.Services.AddSingleton<MailSubmissionService>();

builder.Services.AddSingleton<TeamCatalogStore>();

var personnelOptions = builder.Configuration.GetSection("PersonnelDirectory").Get<PersonnelDirectoryOptions>() ?? new PersonnelDirectoryOptions();
builder.Services.AddHttpClient<IPersonnelDirectoryClient, PersonnelDirectoryClient>(client =>
{
    if (!string.IsNullOrWhiteSpace(personnelOptions.BaseUrl))
        client.BaseAddress = new Uri(personnelOptions.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(personnelOptions.TimeoutSeconds > 0 ? personnelOptions.TimeoutSeconds : 5);
});

// Kuyruk tuketicisi ve takim katalogu senkronizasyonu, uygulama ile birlikte ayaga kalkan
// arka plan servisleridir (IHostedService).
builder.Services.AddHostedService<TeamCatalogSyncService>();
builder.Services.AddHostedService<MailQueueProcessor>();

var app = builder.Build();

// Ilk kurulumda dbo.RelaySettings satiri yoksa appsettings.json > SmtpSettings ile tohumlar.
// Sonrasinda gercek ayarlar her zaman admin panelinden yonetilen veritabanindan okunur.
using (var scope = app.Services.CreateScope())
{
    var smtpOptions = scope.ServiceProvider.GetRequiredService<IOptions<SmtpOptions>>().Value;
    var relaySettingsRepository = scope.ServiceProvider.GetRequiredService<RelaySettingsRepository>();
    try
    {
        await relaySettingsRepository.EnsureSeedAsync(smtpOptions);
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning(ex, "RelaySettings baslangic tohumlamasi yapilamadi (veritabani henuz erisilebilir olmayabilir). Admin panelinden manuel olarak ayarlanabilir.");
    }
}

app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "ok", timeUtc = DateTime.UtcNow }));

app.MapMailEndpoints();
app.MapAdminEndpoints();

app.Run();
