using Microsoft.Extensions.Configuration;

namespace Kesinti.Core.Configuration;

/// <summary>
/// Konsol uygulamalari icin ortak yapilandirma yukleyici.
/// Sirasiyla appsettings.json, appsettings.{ortam}.json ve ortam degiskenlerini okur;
/// ortam degiskenleri her zaman dosya ayarlarinin uzerine yazar.
/// </summary>
public static class AppConfig
{
    public static IConfigurationRoot Load(string basePath, string[]? args = null)
    {
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production";

        var builder = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        if (args is { Length: > 0 })
        {
            builder.AddCommandLine(args);
        }

        return builder.Build();
    }

    public static LiteLlmOptions GetLiteLlm(IConfiguration configuration)
    {
        var options = new LiteLlmOptions();
        configuration.GetSection(LiteLlmOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            throw new InvalidOperationException(
                $"LiteLLM BaseUrl tanimli degil. '{LiteLlmOptions.SectionName}:BaseUrl' ayarini appsettings.json veya LiteLlm__BaseUrl ortam degiskenini kullanarak tanimlayin.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            throw new InvalidOperationException(
                $"LiteLLM ApiKey tanimli degil. '{LiteLlmOptions.SectionName}:ApiKey' ayarini appsettings.json veya LiteLlm__ApiKey ortam degiskenini kullanarak tanimlayin.");
        }

        return options;
    }

    public static DatabaseOptions GetDatabase(IConfiguration configuration)
    {
        var options = new DatabaseOptions();
        configuration.GetSection(DatabaseOptions.SectionName).Bind(options);

        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            throw new InvalidOperationException(
                $"Veritabani baglanti dizesi tanimli degil. '{DatabaseOptions.SectionName}:ConnectionString' ayarini appsettings.json veya Database__ConnectionString ortam degiskenini kullanarak tanimlayin.");
        }

        return options;
    }
}
