using System.Windows;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;

namespace Qylent.Kutuphane.App;

public partial class OperatorLoginWindow : Window
{
    private readonly IOperatorSessionService _sessions;
    private OperatorLoginState? _state;

    public OperatorLoginWindow(IOperatorSessionService sessions)
    {
        InitializeComponent();
        _sessions = sessions;
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        _state = await _sessions.GetLoginStateAsync();
        if (_state.Mode == OperatorMode.Shared)
        {
            var result = await _sessions.LoginAsync(null, null);
            DialogResult = result.Success;
            return;
        }

        OperatorBox.ItemsSource = _state.Operators;
        OperatorBox.SelectedIndex = _state.Operators.Count > 0 ? 0 : -1;
        PinPanel.Visibility = _state.Mode == OperatorMode.Pin ? Visibility.Visible : Visibility.Collapsed;
        ExplanationText.Text = _state.Mode == OperatorMode.Pin
            ? "Görevlinizi seçip PIN kodunuzu girin."
            : "İşlemlerin kimin tarafından yapıldığını kaydetmek için görevlinizi seçin.";
        if (_state.Mode == OperatorMode.Pin)
            PinBox.Focus();
        else
            OperatorBox.Focus();
        if (_state.Operators.Count == 0)
        {
            ErrorText.Text = "Aktif görevli bulunamadı. Yönetici ayarlarından en az bir görevli etkinleştirilmelidir.";
            LoginButton.IsEnabled = false;
        }
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        if (OperatorBox.SelectedItem is not OperatorChoice selected) { ErrorText.Text = "Bir görevli seçin."; return; }
        LoginButton.IsEnabled = false;
        var result = await _sessions.LoginAsync(selected.Id, PinBox.Password);
        LoginButton.IsEnabled = true;
        if (result.Success) DialogResult = true;
        else { ErrorText.Text = result.Message; PinBox.Clear(); PinBox.Focus(); }
    }
}
