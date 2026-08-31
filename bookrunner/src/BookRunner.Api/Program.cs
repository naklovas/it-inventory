using System.Text.Json.Serialization;
using BookRunner.Api.Authorization;
using BookRunner.Api.Hubs;
using BookRunner.Api.Identity;
using BookRunner.Api.Middleware;
using BookRunner.Api.Realtime;
using BookRunner.Application;
using BookRunner.Application.Abstractions;
using BookRunner.Application.Security;
using BookRunner.Infrastructure;
using BookRunner.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Negotiate;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Sunucuya ozel gercek degerler (baglanti dizeleri, AD domaini, personel
// servisi adresi...) appsettings.json'a DEGIL buraya yazilir. Bu dosya
// appsettings.Local.json.example'dan kopyalanir, .gitignore'dadir ve git pull
// hicbir zaman uzerine yazmaz - boylece her guncellemede yeniden girmeye
// gerek kalmaz. En sonda eklendigi icin appsettings.json ve
// appsettings.{Environment}.json degerlerinin hepsinin uzerine yazar.
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: true);

// "Local exe" dagitimi: uygulama Windows servisi olarak da calisabilsin.
builder.Host.UseWindowsService(options => options.ServiceName = "BookRunner API");

// ---------------------------------------------------------------------------
// Kimlik dogrulama: Windows Entegre Kimlik Dogrulama (Kerberos/NTLM).
// Kullanici ayrica oturum acmaz; tarayici Windows kimligini iletir.
// ---------------------------------------------------------------------------
builder.Services
    .AddAuthentication(NegotiateDefaults.AuthenticationScheme)
    .AddNegotiate();

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build());

// Her izin icin bir politika: [Authorize(Policy = Permissions.RunbookWrite)]
foreach (var permission in Permissions.All)
{
    builder.Services.AddAuthorizationBuilder()
        .AddPolicy(permission, policy => policy.RequireClaim(Permissions.ClaimType, permission));
}

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, HttpCurrentUser>();
builder.Services.AddScoped<IClaimsTransformation, BookRunnerClaimsTransformation>();

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// SignalR canli isbirligi bildirimleri; Infrastructure'daki bos uygulamanin yerini alir.
builder.Services.AddSignalR();
builder.Services.AddScoped<IRealtimeNotifier, SignalRRealtimeNotifier>();

builder.Services
    .AddControllers(options => options.SuppressAsyncSuffixInActionNames = true)
    .AddJsonOptions(options =>
    {
        // Enum'lar istemciye okunabilir metin olarak gider.
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    });

builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.SuppressInferBindingSourcesForParameters = false;
});

// ---------------------------------------------------------------------------
// Swagger / OpenAPI
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "BookRunner API",
        Version = "v1",
        Description = """
            Runbook hazirlama ve isbirligi platformunun REST API'si.

            Kimlik dogrulama Windows Entegre Kimlik Dogrulama (Negotiate) ile yapilir.
            Swagger UI'yi etki alanina uye bir makinede, kurumsal tarayiciyla acin;
            kimlik bilgileri otomatik iletilir.
            """,
        Contact = new OpenApiContact { Name = "BookRunner" }
    });

    options.AddSecurityDefinition("windows", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "negotiate",
        Description = "Windows Entegre Kimlik Dogrulama"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        [new OpenApiSecurityScheme
        {
            Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "windows" }
        }] = Array.Empty<string>()
    });

    // XML yorumlarindan uc nokta aciklamalari.
    var xmlPath = Path.Combine(AppContext.BaseDirectory, $"{typeof(Program).Assembly.GetName().Name}.xml");
    if (File.Exists(xmlPath))
    {
        options.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);
    }

    foreach (var assembly in new[] { "BookRunner.Application", "BookRunner.Domain" })
    {
        var path = Path.Combine(AppContext.BaseDirectory, $"{assembly}.xml");
        if (File.Exists(path))
        {
            options.IncludeXmlComments(path);
        }
    }
});

// Ayri barinan frontend'in API'yi kimlik bilgileriyle cagirabilmesi icin CORS.
const string WebCorsPolicy = "BookRunnerWeb";
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy(WebCorsPolicy, policy => policy
        .WithOrigins(allowedOrigins)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

builder.Services.AddHealthChecks()
    .AddDbContextCheck<BookRunnerDbContext>("database");

var app = builder.Build();

// Sema guncellemesi ve rol eslemesi tohumlamasi.
if (app.Configuration.GetValue("Database:MigrateOnStartup", true))
{
    await app.Services.InitializeDatabaseAsync();
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "BookRunner API v1");
    options.DocumentTitle = "BookRunner API";
    options.DisplayRequestDuration();
});

app.UseCors(WebCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<RunbookHub>("/hubs/runbook");
app.MapHealthChecks("/health").AllowAnonymous();

app.Run();

/// <summary>Swagger XML dosyasi adini cozebilmek icin acik sinif.</summary>
public partial class Program;
