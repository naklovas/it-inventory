using System.Net;
using System.Text.Json.Serialization;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Sunucuya ozel gercek degerler buraya yazilir (bkz. BookRunner.Api/Program.cs
// ayni satir). .gitignore'dadir; git pull hicbir zaman uzerine yazmaz.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// "Local exe" dagitimi icin Windows servisi destegi.
builder.Host.UseWindowsService(options => options.ServiceName = "BookRunner Web");

// Windows Entegre Kimlik Dogrulama: kullanici ayrica oturum acmaz.
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

builder.Services.Configure<ApiOptions>(builder.Configuration.GetSection(ApiOptions.SectionName));

var apiOptions = builder.Configuration.GetSection(ApiOptions.SectionName).Get<ApiOptions>() ?? new ApiOptions();

builder.Services.AddTransient<ApiConnectionHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<WindowsIdentityHandler>();

// API cagrilari kullanicinin Windows kimligiyle yapilir.
// Not: Web ve API ayri sunuculardaysa Kerberos kisitlanmis yetki devri
// (constrained delegation) yapilandirilmalidir; bkz. README.
builder.Services
    .AddHttpClient<BookRunnerApiClient>(client =>
    {
        client.BaseAddress = new Uri(apiOptions.BaseUrl.TrimEnd('/') + "/");
        client.Timeout = TimeSpan.FromSeconds(apiOptions.TimeoutSeconds);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        UseDefaultCredentials = true,
        Credentials = CredentialCache.DefaultNetworkCredentials,
        AllowAutoRedirect = false
    })
    // ASP.NET Core, klasik ASP.NET'in aksine kimligi dogrulanmis kullaniciyi
    // otomatik impersonate etmez; bu handler olmadan UseDefaultCredentials
    // yukarida surecin kendi kimligini (IIS'te uygulama havuzu hesabi)
    // gonderir, tarayicidaki kullaniciyi degil.
    .AddHttpMessageHandler<WindowsIdentityHandler>()
    // API'ye hic ulasilamamasi (baglanti reddi, zaman asimi) durumunu
    // controller'larin zaten yakaladigi ApiException'a cevirir.
    .AddHttpMessageHandler<ApiConnectionHandler>();

// API'ye giden JSON uclarindaki (gorev ekleme, atama, durum degistirme...)
// enum degerleri metin olarak gonderilir (orn. "priority": "Normal"). API
// tarafi bunu JsonStringEnumConverter ile kabul ediyor; Web'in kendi model
// binder'i da ayni cevirici olmadan bu govdeleri cozemez ve istek API'ye
// bozuk/varsayilan degerlerle ulasip "One or more validation errors
// occurred" hatasi doner.
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
