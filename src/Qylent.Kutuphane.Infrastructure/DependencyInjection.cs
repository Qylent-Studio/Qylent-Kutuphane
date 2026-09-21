using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;
using Qylent.Kutuphane.Infrastructure.Services;

namespace Qylent.Kutuphane.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddQylentKutuphaneInfrastructure(this IServiceCollection services, string? dataDirectory = null)
    {
        var paths = new AppPaths(dataDirectory);
        services.AddSingleton(paths);
        services.AddPooledDbContextFactory<LibraryDbContext>(options =>
            options.UseSqlite($"Data Source={paths.DatabasePath};Cache=Shared;Foreign Keys=True;Pooling=False"));
        services.AddSingleton<IKeyProtectionProvider, DpapiKeyProtectionProvider>();
        services.AddSingleton<SensitiveDataProtector>();
        services.AddSingleton<DatabaseInitializer>();
        services.AddSingleton<ISetupService, SetupService>();
        services.AddSingleton<ISecurityService, SecurityService>();
        services.AddSingleton<ICatalogService, CatalogService>();
        services.AddSingleton<IMemberService, MemberService>();
        services.AddSingleton<ICirculationService, CirculationService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<IBackupService, BackupService>();
        services.AddSingleton<AutomaticBackupCoordinator>();
        services.AddSingleton<IAdministrationService, AdministrationService>();
        return services;
    }
}
