using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qylent.Kutuphane.App.ViewModels;
using Qylent.Kutuphane.App.Ui;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Infrastructure;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Services;

namespace Qylent.Kutuphane.App;

public partial class App : Application
{
    private IHost? _host;
    private Mutex? _singleInstance;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = ShutdownMode.OnExplicitShutdown;
        _singleInstance = new Mutex(true, "Qylent.Kutuphane.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            MessageBox.Show("Qylent Kütüphane zaten açık.", "Qylent Kütüphane", MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

        _host = Host.CreateDefaultBuilder()
            .ConfigureServices(services =>
            {
                services.AddQylentKutuphaneInfrastructure();
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<IThemeService, ThemeService>();
                services.AddSingleton<MainWindow>();
                services.AddTransient<SetupWindow>();
                services.AddTransient<OperatorLoginWindow>();
            })
            .Build();
        await _host.StartAsync();
        await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        if (!await _host.Services.GetRequiredService<ISetupService>().IsSetupCompletedAsync())
        {
            var setup = _host.Services.GetRequiredService<SetupWindow>();
            if (setup.ShowDialog() != true) { Shutdown(); return; }
        }
        var profile = await _host.Services.GetRequiredService<IAdministrationService>().GetLibraryProfileAsync();
        _host.Services.GetRequiredService<IThemeService>().Apply(profile?.ThemePreference ?? Core.Domain.ThemePreference.System);
        var operatorLogin = _host.Services.GetRequiredService<OperatorLoginWindow>();
        if (operatorLogin.ShowDialog() != true) { Shutdown(); return; }
        await _host.Services.GetRequiredService<AutomaticBackupCoordinator>().RunIfDueAsync();
        MainWindow = _host.Services.GetRequiredService<MainWindow>();
        MainWindow.Show();
        ShutdownMode = ShutdownMode.OnMainWindowClose;
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        if (_host is not null)
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        _singleInstance?.ReleaseMutex();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
