using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Qylent.Kutuphane.App.ViewModels;

namespace Qylent.Kutuphane.App;

public partial class MainWindow : Window
{
    private MainViewModel ViewModel => (MainViewModel)DataContext;
    public MainWindow(MainViewModel viewModel) { InitializeComponent(); DataContext = viewModel; }
    private void Window_Loaded(object sender, RoutedEventArgs e) => BarcodeBox.Focus();
    private void BarcodeBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) ViewModel.CheckoutCommand.Execute(null); }
    private async void AdminButton_Click(object sender, RoutedEventArgs e)
    {
        if (!ViewModel.IsAdminUnlocked)
        {
            var dialog = new AdminLoginWindow(ViewModel) { Owner = this };
            if (dialog.ShowDialog() != true || !await ViewModel.UnlockAdminAsync(dialog.Password)) return;
        }
        ViewModel.Page = "Admin";
    }
    private void LockAdmin_Click(object sender, RoutedEventArgs e) => ViewModel.LockAdmin();
    private async void CreateBackup_Click(object sender, RoutedEventArgs e) => await ViewModel.CreateBackupAsync(BackupPasswordBox.Password);
    private async void RestoreBackup_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Title = "Qylent Kütüphane yedeğini seçin", Filter = "Qylent yedeği (*.qylibbackup)|*.qylibbackup" };
        if (dialog.ShowDialog(this) == true) await ViewModel.RestoreBackupAsync(dialog.FileName, BackupPasswordBox.Password);
    }
}
