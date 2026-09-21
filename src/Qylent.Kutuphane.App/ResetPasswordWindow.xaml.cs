using System.Windows;
using Qylent.Kutuphane.App.ViewModels;

namespace Qylent.Kutuphane.App;

public partial class ResetPasswordWindow : Window
{
    private readonly MainViewModel _viewModel;
    public ResetPasswordWindow(MainViewModel viewModel) { _viewModel = viewModel; InitializeComponent(); }
    private async void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (NewPassword.Password != NewPasswordAgain.Password) { ResultText.Text = "Yeni şifreler eşleşmiyor."; return; }
        var result = await _viewModel.ResetAdminPasswordAsync([Answer1.Text, Answer2.Text, Answer3.Text], NewPassword.Password);
        ResultText.Text = result.Message;
        if (result.Success) { DialogResult = true; Close(); }
    }
}
