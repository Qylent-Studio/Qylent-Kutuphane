using System.Threading;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Qylent.Kutuphane.App.ViewModels;
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
                services.AddSingleton<MainWindow>();
                services.AddTransient<SetupWindow>();
            })
            .Build();
        await _host.StartAsync();
        await _host.Services.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        if (!await _host.Services.GetRequiredService<ISetupService>().IsSetupCompletedAsync())
        {
            var setup = _host.Services.GetRequiredService<SetupWindow>();
            if (setup.ShowDialog() != true) { Shutdown(); return; }
        }
        await _host.Services.GetRequiredService<AutomaticBackupCoordinator>().RunIfDueAsync();
        _host.Services.GetRequiredService<MainWindow>().Show();
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
