using System.Windows;
using System.Windows.Input;
using Qylent.Kutuphane.App.ViewModels;

namespace Qylent.Kutuphane.App;

public partial class AdminLoginWindow : Window
{
    private readonly MainViewModel _viewModel;
    public AdminLoginWindow(MainViewModel viewModel) { _viewModel = viewModel; InitializeComponent(); Loaded += (_, _) => PasswordInput.Focus(); }
    public string Password => PasswordInput.Password;
    private void Login_Click(object sender, RoutedEventArgs e) { DialogResult = true; Close(); }
    private void PasswordInput_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { DialogResult = true; Close(); } }
    private void ResetPassword_Click(object sender, RoutedEventArgs e) => new ResetPasswordWindow(_viewModel) { Owner = this }.ShowDialog();
}
