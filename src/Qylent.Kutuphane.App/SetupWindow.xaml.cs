using System.Windows;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.App;

public partial class SetupWindow : Window
{
    private readonly ISetupService _setup;
    public SetupWindow(ISetupService setup, AppPaths paths) { _setup = setup; InitializeComponent(); BackupDirectory.Text = paths.DefaultBackupDirectory; }
    private void Back_Click(object sender, RoutedEventArgs e) { if (Steps.SelectedIndex > 0) Steps.SelectedIndex--; UpdateButtons(); }
    private void LibraryType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
    {
        if (ProfileDescription is not null)
            ProfileDescription.Text = LibraryProfileRules.Description((LibraryType)Math.Clamp(LibraryType.SelectedIndex, 0, 3));
    }
    private async void Next_Click(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = string.Empty;
        if (Steps.SelectedIndex < 2) { Steps.SelectedIndex++; UpdateButtons(); return; }
        if (AdminPassword.Password != AdminPasswordAgain.Password) { ErrorText.Text = "Şifreler eşleşmiyor."; return; }
        if (!int.TryParse(LoanDays.Text, out var days) || !int.TryParse(MaxLoans.Text, out var max) || !int.TryParse(MaxRenewals.Text, out var renewals)) { ErrorText.Text = "Ödünç kurallarına geçerli sayılar girin."; return; }
        NextButton.IsEnabled = false;
        var result = await _setup.CompleteSetupAsync(new SetupRequest(LibraryName.Text, null, (LibraryType)LibraryType.SelectedIndex, (OperatorMode)OperatorMode.SelectedIndex,
            days, max, renewals, BackupDirectory.Text, AdminPassword.Password,
            [new("İlk okulunuz?", Answer1.Text), new("En sevdiğiniz kitap?", Answer2.Text), new("Doğduğunuz şehir?", Answer3.Text)],
            InitialOperatorName.Text, InitialOperatorPin.Password));
        if (!result.Success) { ErrorText.Text = result.Message; NextButton.IsEnabled = true; return; }
        DialogResult = true; Close();
    }
    private void UpdateButtons() { BackButton.IsEnabled = Steps.SelectedIndex > 0; NextButton.Content = Steps.SelectedIndex == 2 ? "Kurulumu tamamla" : "İleri"; }
}
