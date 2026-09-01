using BookRunner.Application.Abstractions;
using BookRunner.Application.Common;
using BookRunner.Infrastructure.Audit;
using BookRunner.Infrastructure.Directory;
using BookRunner.Infrastructure.Email;
using BookRunner.Infrastructure.Export;
using BookRunner.Infrastructure.Identity;
using BookRunner.Infrastructure.Integration;
using BookRunner.Infrastructure.Personnel;
using BookRunner.Infrastructure.Persistence;
using BookRunner.Infrastructure.Persistence.Interceptors;
using BookRunner.Infrastructure.Realtime;
using BookRunner.Infrastructure.Scripting;
using BookRunner.Infrastructure.ServiceManager;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace BookRunner.Infrastructure;

/// <summary>Altyapi servislerinin kaydi (veritabani, AD, e-posta, disa aktarim, entegrasyon).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddMemoryCache();

        services.AddOptions<ActiveDirectoryOptions>()
            .Bind(configuration.GetSection(ActiveDirectoryOptions.SectionName))
            .ValidateDataAnnotations();

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations();

        services.Configure<RoleOptions>(configuration.GetSection(RoleOptions.SectionName));
        services.Configure<ServiceManagerOptions>(configuration.GetSection(ServiceManagerOptions.SectionName));

        // Tum SQL Server baglanti dizeleri appsettings icindeki "ConnectionStrings"
        // bolumunde toplansin diye, SCSM baglantisi de oradan okunabilir.
        services.PostConfigure<ServiceManagerOptions>(options =>
        {
            if (string.IsNullOrWhiteSpace(options.ConnectionString))
            {
                options.ConnectionString = configuration.GetConnectionString("ServiceManager") ?? string.Empty;
            }
        });
        services.Configure<ScriptingOptions>(configuration.GetSection(ScriptingOptions.SectionName));
        services.Configure<IntegrationOptions>(configuration.GetSection(IntegrationOptions.SectionName));
        services.Configure<PersonnelDirectoryOptions>(configuration.GetSection(PersonnelDirectoryOptions.SectionName));
        services.Configure<GamificationOptions>(configuration.GetSection(GamificationOptions.SectionName));

        services.AddScoped<AuditSaveChangesInterceptor>();

        services.AddDbContext<BookRunnerDbContext>((provider, options) =>
        {
            options.UseSqlServer(
                configuration.GetConnectionString("BookRunner"),
                sql =>
                {
                    sql.MigrationsHistoryTable("__EFMigrationsHistory", "bookrunner");
                    // Gecici ag/SQL hatalarinda EF kendi yeniden dener.
                    sql.EnableRetryOnFailure(3, TimeSpan.FromSeconds(5), null);
                });

            options.AddInterceptors(provider.GetRequiredService<AuditSaveChangesInterceptor>());
        });

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<BookRunnerDbContext>());
        services.AddScoped<DatabaseInitializer>();

        services.AddScoped<IAuditService, AuditService>();
        services.AddScoped<IDirectoryService, ActiveDirectoryService>();
        services.AddScoped<IEmailSender, OutboxEmailSender>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<IExcelService, ExcelService>();
        services.AddScoped<IPdfService, PdfService>();
        services.AddScoped<IServiceManagerReader, ServiceManagerReader>();
        services.AddScoped<IScriptRunner, RoslynScriptRunner>();

        // API projesi SignalR tabanli uygulamayi kaydederek bunu degistirir.
        services.AddScoped<IRealtimeNotifier, NullRealtimeNotifier>();

        AddIntegration(services, configuration);
        AddPersonnelDirectory(services, configuration);

        services.AddHostedService<EmailOutboxProcessor>();
        services.AddHostedService<TeamCatalogSyncService>();

        // QuestPDF Community lisansi: kurum ici, ucretsiz kullanim icin.
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

        return services;
    }

    private static void AddIntegration(IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(IntegrationOptions.SectionName);
        var options = section.Get<IntegrationOptions>() ?? new IntegrationOptions();

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<IExternalIntegrationClient, NullIntegrationClient>();
            return;
        }

        services.AddHttpClient<IExternalIntegrationClient, ExternalIntegrationClient>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl);
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);

            if (!string.IsNullOrWhiteSpace(options.ApiKey))
            {
                client.DefaultRequestHeaders.Add(options.ApiKeyHeader, options.ApiKey);
            }
        });
    }

    private static void AddPersonnelDirectory(IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(PersonnelDirectoryOptions.SectionName).Get<PersonnelDirectoryOptions>()
            ?? new PersonnelDirectoryOptions();

        if (!options.Enabled || string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            services.AddScoped<IPersonnelDirectoryService, NullPersonnelDirectoryService>();
            return;
        }

        services.AddHttpClient<IPersonnelDirectoryService, PersonnelDirectoryService>(client =>
        {
            client.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            client.Timeout = TimeSpan.FromSeconds(options.TimeoutSeconds);
        });
    }
}
