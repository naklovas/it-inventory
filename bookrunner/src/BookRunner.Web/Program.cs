using System.Net;
using BookRunner.Web.Services;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

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
    });

builder.Services.AddControllersWithViews();
builder.Services.AddHttpContextAccessor();

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
