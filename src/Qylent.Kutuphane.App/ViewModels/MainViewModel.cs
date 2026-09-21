using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows.Input;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.App.ViewModels;

public sealed class MainViewModel : INotifyPropertyChanged
{
    private readonly ICirculationService _circulation;
    private readonly ICatalogService _catalog;
    private readonly IMemberService _members;
    private readonly IReportService _reports;
    private readonly IBackupService _backups;
    private readonly ISecurityService _security;
    private readonly IAdministrationService _administration;
    private readonly AppPaths _paths;
    private string _page = "Dolaşım";
    private string _statusMessage = "Barkodu okutun veya menüden bir işlem seçin.";
    private bool _statusIsError;
    private string _memberNumber = string.Empty;
    private string _barcode = string.Empty;
    private string _catalogQuery = string.Empty;
    private string _memberQuery = string.Empty;
    private BookSearchResult? _selectedBook;
    private LoanSummary? _selectedLoan;
    private MemberSummary? _selectedMember;
    private ReportPreset? _selectedReportPreset;
    private AdminSession? _adminSession;

    public MainViewModel(ICirculationService circulation, ICatalogService catalog, IMemberService members,
        IReportService reports, IBackupService backups, ISecurityService security, IAdministrationService administration, AppPaths paths)
    {
        _circulation = circulation; _catalog = catalog; _members = members; _reports = reports;
        _backups = backups; _security = security; _administration = administration; _paths = paths;
        NavigateCommand = new RelayCommand(x => Page = x?.ToString() ?? "Dolaşım");
        CheckoutCommand = new AsyncRelayCommand(_ => CheckoutAsync());
        ReturnCommand = new AsyncRelayCommand(_ => ReturnAsync());
        SearchCatalogCommand = new AsyncRelayCommand(_ => SearchCatalogAsync());
        SearchMembersCommand = new AsyncRelayCommand(_ => SearchMembersAsync());
        ReserveCommand = new AsyncRelayCommand(_ => ReserveAsync(), _ => SelectedBook is not null);
        RenewCommand = new AsyncRelayCommand(_ => RenewAsync(), _ => SelectedLoan is not null);
        RefreshCommand = new AsyncRelayCommand(_ => RefreshAsync());
        AddBookCommand = new AsyncRelayCommand(_ => AddBookAsync(), _ => IsAdminUnlocked);
        AddMemberCommand = new AsyncRelayCommand(_ => AddMemberAsync(), _ => IsAdminUnlocked);
        ExportReportCommand = new AsyncRelayCommand(_ => ExportReportAsync(), _ => IsAdminUnlocked);
        AddRuleCommand = new AsyncRelayCommand(_ => AddRuleAsync(), _ => IsAdminUnlocked);
        AddOperatorCommand = new AsyncRelayCommand(_ => AddOperatorAsync(), _ => IsAdminUnlocked);
        AddMemberFieldCommand = new AsyncRelayCommand(_ => AddMemberFieldAsync(), _ => IsAdminUnlocked);
        SaveSettingsCommand = new AsyncRelayCommand(_ => SaveSettingsAsync(), _ => IsAdminUnlocked);
        SaveReportPresetCommand = new AsyncRelayCommand(_ => SaveReportPresetAsync(), _ => IsAdminUnlocked);
        ApplyReportPresetCommand = new RelayCommand(_ => ApplyReportPreset(), _ => IsAdminUnlocked && SelectedReportPreset is not null);
        DeleteReportPresetCommand = new AsyncRelayCommand(_ => DeleteReportPresetAsync(), _ => IsAdminUnlocked && SelectedReportPreset is not null);
        LoadSelectedBookCommand = new RelayCommand(_ => LoadSelectedBook(), _ => IsAdminUnlocked && SelectedBook is not null);
        UpdateSelectedBookCommand = new AsyncRelayCommand(_ => UpdateSelectedBookAsync(), _ => IsAdminUnlocked && SelectedBook is not null);
        ArchiveSelectedBookCommand = new AsyncRelayCommand(_ => ArchiveSelectedBookAsync(), _ => IsAdminUnlocked && SelectedBook is not null);
        DeleteSelectedBookCommand = new AsyncRelayCommand(_ => DeleteSelectedBookAsync(), _ => IsAdminUnlocked && SelectedBook is not null);
        LoadSelectedMemberCommand = new RelayCommand(_ => LoadSelectedMember(), _ => IsAdminUnlocked && SelectedMember is not null);
        UpdateSelectedMemberCommand = new AsyncRelayCommand(_ => UpdateSelectedMemberAsync(), _ => IsAdminUnlocked && SelectedMember is not null);
        ArchiveSelectedMemberCommand = new AsyncRelayCommand(_ => ArchiveSelectedMemberAsync(), _ => IsAdminUnlocked && SelectedMember is not null);
        DeleteSelectedMemberCommand = new AsyncRelayCommand(_ => DeleteSelectedMemberAsync(), _ => IsAdminUnlocked && SelectedMember is not null);
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand NavigateCommand { get; }
    public ICommand CheckoutCommand { get; }
    public ICommand ReturnCommand { get; }
    public ICommand SearchCatalogCommand { get; }
    public ICommand SearchMembersCommand { get; }
    public ICommand ReserveCommand { get; }
    public ICommand RenewCommand { get; }
    public ICommand RefreshCommand { get; }
    public ICommand AddBookCommand { get; }
    public ICommand AddMemberCommand { get; }
    public ICommand ExportReportCommand { get; }
    public ICommand AddRuleCommand { get; }
    public ICommand AddOperatorCommand { get; }
    public ICommand AddMemberFieldCommand { get; }
    public ICommand SaveSettingsCommand { get; }
    public ICommand SaveReportPresetCommand { get; }
    public ICommand ApplyReportPresetCommand { get; }
    public ICommand DeleteReportPresetCommand { get; }
    public ICommand LoadSelectedBookCommand { get; }
    public ICommand UpdateSelectedBookCommand { get; }
    public ICommand ArchiveSelectedBookCommand { get; }
    public ICommand DeleteSelectedBookCommand { get; }
    public ICommand LoadSelectedMemberCommand { get; }
    public ICommand UpdateSelectedMemberCommand { get; }
    public ICommand ArchiveSelectedMemberCommand { get; }
    public ICommand DeleteSelectedMemberCommand { get; }

    public ObservableCollection<BookSearchResult> CatalogResults { get; } = [];
    public ObservableCollection<MemberSummary> MemberResults { get; } = [];
    public ObservableCollection<LoanSummary> ActiveLoans { get; } = [];
    public ObservableCollection<LoanSummary> OverdueLoans { get; } = [];
    public ObservableCollection<ReservationSummary> Reservations { get; } = [];
    public ObservableCollection<LoanRule> LoanRules { get; } = [];
    public ObservableCollection<Operator> Operators { get; } = [];
    public ObservableCollection<MemberFieldDefinition> MemberFields { get; } = [];
    public ObservableCollection<AuditSummary> AuditEntries { get; } = [];
    public ObservableCollection<ReportPreset> ReportPresets { get; } = [];
    public ObservableCollection<EditableMemberField> EditableMemberFields { get; } = [];

    public string Page { get => _page; set { if (Set(ref _page, value)) { OnPropertyChanged(nameof(PageTitle)); _ = RefreshAsync(); } } }
    public string PageTitle => Page switch { "Dolaşım" => "Ödünç ve iade", "Katalog" => "Katalog", "Üyeler" => "Üyeler", "Ayırtmalar" => "Ayırtmalar", "Gecikenler" => "Gecikenler", _ => "Yönetim" };
    public string StatusMessage { get => _statusMessage; set => Set(ref _statusMessage, value); }
    public bool StatusIsError { get => _statusIsError; set => Set(ref _statusIsError, value); }
    public string MemberNumber { get => _memberNumber; set => Set(ref _memberNumber, value); }
    public string Barcode { get => _barcode; set => Set(ref _barcode, value); }
    public string CatalogQuery { get => _catalogQuery; set => Set(ref _catalogQuery, value); }
    public string MemberQuery { get => _memberQuery; set => Set(ref _memberQuery, value); }
    public BookSearchResult? SelectedBook { get => _selectedBook; set { if (Set(ref _selectedBook, value)) RaiseCommands(); } }
    public LoanSummary? SelectedLoan { get => _selectedLoan; set { if (Set(ref _selectedLoan, value)) RaiseCommands(); } }
    public MemberSummary? SelectedMember { get => _selectedMember; set { if (Set(ref _selectedMember, value)) RaiseCommands(); } }
    public ReportPreset? SelectedReportPreset { get => _selectedReportPreset; set { if (Set(ref _selectedReportPreset, value)) RaiseCommands(); } }
    public bool IsAdminUnlocked => _security.IsAdminSessionValid(_adminSession);

    public string NewBookTitle { get; set; } = string.Empty;
    public string NewBookAuthors { get; set; } = string.Empty;
    public string NewBookIsbn { get; set; } = string.Empty;
    public string NewBookCategory { get; set; } = string.Empty;
    public string NewBookBarcode { get; set; } = string.Empty;
    public string NewBookShelf { get; set; } = string.Empty;
    public string NewMemberNumber { get; set; } = string.Empty;
    public string NewMemberName { get; set; } = string.Empty;
    public string NewMemberType { get; set; } = "Genel";
    public string NewMemberUnit { get; set; } = string.Empty;
    public DateTime ReportStart { get; set; } = DateTime.Today;
    public DateTime ReportEnd { get; set; } = DateTime.Today;
    public int ReportPeriodIndex { get; set; } = 3;
    public bool IncludeOverdueSheet { get; set; } = true;
    public bool IncludeReservationsSheet { get; set; } = true;
    public bool IncludeReportDate { get; set; } = true;
    public bool IncludeReportActivity { get; set; } = true;
    public bool IncludeReportOperator { get; set; } = true;
    public bool IncludeReportEntity { get; set; } = true;
    public bool IncludeReportDescription { get; set; } = true;
    public string ReportPresetName { get; set; } = string.Empty;
    public string NewRuleName { get; set; } = string.Empty;
    public string NewRuleMemberType { get; set; } = string.Empty;
    public string NewRuleCategory { get; set; } = string.Empty;
    public string NewRuleDays { get; set; } = "14";
    public string NewRuleMaxLoans { get; set; } = "5";
    public string NewRuleMaxRenewals { get; set; } = "1";
    public string NewOperatorName { get; set; } = string.Empty;
    public string NewOperatorPin { get; set; } = string.Empty;
    public string NewFieldName { get; set; } = string.Empty;
    public int NewFieldTypeIndex { get; set; }
    public bool NewFieldRequired { get; set; }
    public bool NewFieldSensitive { get; set; }
    public string NewFieldChoices { get; set; } = string.Empty;
    public string SettingsLibraryName { get; set; } = string.Empty;
    public string SettingsLogoPath { get; set; } = string.Empty;
    public int SettingsLibraryTypeIndex { get; set; }
    public int SettingsOperatorModeIndex { get; set; }
    public string SettingsLoanDays { get; set; } = "14";
    public string SettingsMaxLoans { get; set; } = "5";
    public string SettingsMaxRenewals { get; set; } = "1";
    public string SettingsBackupDirectory { get; set; } = string.Empty;

    public async Task<bool> UnlockAdminAsync(string password)
    {
        var result = await _security.LoginAdminAsync(password);
        _adminSession = result.Value;
        Status(result.Success, result.Message);
        OnPropertyChanged(nameof(IsAdminUnlocked)); RaiseCommands();
        return result.Success;
    }

    public async Task<OperationResult> ResetAdminPasswordAsync(IReadOnlyList<string> answers, string newPassword)
        => await _security.ResetAdminPasswordAsync(answers, newPassword);

    public void LockAdmin()
    {
        _adminSession = null; OnPropertyChanged(nameof(IsAdminUnlocked)); RaiseCommands(); Page = "Dolaşım";
        Status(true, "Yönetici oturumu kilitlendi.");
    }

    public async Task CreateBackupAsync(string password)
    {
        if (!EnsureAdmin()) return;
        var directory = string.IsNullOrWhiteSpace(SettingsBackupDirectory) ? _paths.DefaultBackupDirectory : SettingsBackupDirectory;
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"qylent-kutuphane-{DateTime.Now:yyyyMMdd-HHmmss}.qylibbackup");
        var result = await _backups.CreateAsync(new(path, password, false)); Status(result.Success, result.Message + (result.Success ? $" {path}" : string.Empty));
    }

    public async Task RestoreBackupAsync(string file, string password)
    {
        if (!EnsureAdmin()) return;
        var result = await _backups.RestoreAsync(new(file, password)); Status(result.Success, result.Message);
        if (result.Success) await RefreshAsync();
    }

    private async Task CheckoutAsync()
    {
        var result = await _circulation.CheckoutAsync(new(MemberNumber, Barcode)); Status(result.Success, result.Message);
        if (result.Success) { Barcode = string.Empty; await RefreshAsync(); }
    }

    private async Task ReturnAsync()
    {
        var result = await _circulation.ReturnAsync(new(Barcode)); Status(result.Success, result.Message);
        if (result.Success) { Barcode = string.Empty; await RefreshAsync(); }
    }

    private async Task ReserveAsync()
    {
        if (SelectedBook is null) return;
        var result = await _circulation.ReserveAsync(new(MemberNumber, SelectedBook.BookTitleId)); Status(result.Success, result.Message);
        if (result.Success) await RefreshAsync();
    }

    private async Task RenewAsync()
    {
        if (SelectedLoan is null) return;
        var result = await _circulation.RenewAsync(SelectedLoan.LoanId); Status(result.Success, result.Message);
        if (result.Success) await RefreshAsync();
    }

    private async Task SearchCatalogAsync() => Replace(CatalogResults, await _catalog.SearchAsync(CatalogQuery));
    private async Task SearchMembersAsync() => Replace(MemberResults, await _members.SearchAsync(MemberQuery));

    private async Task RefreshAsync()
    {
        Replace(ActiveLoans, await _circulation.GetActiveLoansAsync());
        Replace(OverdueLoans, await _circulation.GetActiveLoansAsync(true));
        Replace(Reservations, await _circulation.GetReservationsAsync());
        if (Page == "Katalog") await SearchCatalogAsync();
        if (Page == "Üyeler") await SearchMembersAsync();
        if (Page == "Admin" && IsAdminUnlocked) await LoadAdministrationAsync();
    }

    private async Task AddBookAsync()
    {
        if (!EnsureAdmin()) return;
        var title = await _catalog.AddTitleAsync(new(NewBookTitle, NewBookAuthors, Null(NewBookIsbn), null, null, Null(NewBookCategory), "Türkçe", null), "Yönetici");
        if (!title.Success || title.Value is null) { Status(false, title.Message); return; }
        var copy = await _catalog.AddCopyAsync(new(title.Value.Id, NewBookBarcode, Null(NewBookShelf)), "Yönetici"); Status(copy.Success, copy.Message);
        if (copy.Success) { NewBookTitle = NewBookAuthors = NewBookIsbn = NewBookCategory = NewBookBarcode = NewBookShelf = string.Empty; OnPropertyChanged(string.Empty); await SearchCatalogAsync(); }
    }

    private async Task AddMemberAsync()
    {
        if (!EnsureAdmin()) return;
        var result = await _members.AddAsync(new(NewMemberNumber, NewMemberName, NewMemberType, Null(NewMemberUnit), null, null, null, BuildCustomFields(true)), "Yönetici"); Status(result.Success, result.Message);
        if (result.Success) { NewMemberNumber = NewMemberName = NewMemberUnit = string.Empty; OnPropertyChanged(string.Empty); await SearchMembersAsync(); }
    }

    private void LoadSelectedBook()
    {
        if (SelectedBook is null) return;
        NewBookTitle = SelectedBook.Title; NewBookAuthors = SelectedBook.Authors; NewBookIsbn = SelectedBook.Isbn ?? string.Empty;
        NewBookBarcode = SelectedBook.Barcode ?? string.Empty; NewBookShelf = SelectedBook.ShelfLocation ?? string.Empty; OnPropertyChanged(string.Empty);
        Status(true, "Seçili kitap düzenleme alanına yüklendi.");
    }

    private async Task UpdateSelectedBookAsync()
    {
        if (SelectedBook is null) return;
        var title = await _catalog.UpdateTitleAsync(SelectedBook.BookTitleId, new(NewBookTitle, NewBookAuthors, Null(NewBookIsbn), null, null, Null(NewBookCategory), "Türkçe", null), "Yönetici");
        if (!title.Success) { Status(false, title.Message); return; }
        if (SelectedBook.BookCopyId is Guid copyId)
        {
            var copy = await _catalog.UpdateCopyAsync(copyId, NewBookBarcode, Null(NewBookShelf), "Yönetici"); if (!copy.Success) { Status(false, copy.Message); return; }
        }
        Status(true, "Kitap kaydı güncellendi."); await SearchCatalogAsync();
    }

    private async Task ArchiveSelectedBookAsync()
    {
        if (SelectedBook is null) return;
        var result = SelectedBook.BookCopyId is Guid copyId ? await _catalog.ArchiveCopyAsync(copyId, "Yönetici") : await _catalog.ArchiveTitleAsync(SelectedBook.BookTitleId, "Yönetici");
        Status(result.Success, result.Message); if (result.Success) await SearchCatalogAsync();
    }

    private async Task DeleteSelectedBookAsync()
    {
        if (SelectedBook is null) return;
        var result = SelectedBook.BookCopyId is Guid copyId ? await _catalog.DeleteCopyAsync(copyId, "Yönetici") : await _catalog.DeleteTitleAsync(SelectedBook.BookTitleId, "Yönetici");
        Status(result.Success, result.Message); if (result.Success) await SearchCatalogAsync();
    }

    private void LoadSelectedMember()
    {
        if (SelectedMember is null) return;
        NewMemberNumber = SelectedMember.MemberNumber; NewMemberName = SelectedMember.FullName; NewMemberType = SelectedMember.MemberType; NewMemberUnit = SelectedMember.ClassOrUnit ?? string.Empty;
        OnPropertyChanged(string.Empty); Status(true, "Seçili üye düzenleme alanına yüklendi.");
    }

    private async Task UpdateSelectedMemberAsync()
    {
        if (SelectedMember is null) return;
        var result = await _members.UpdateAsync(SelectedMember.Id, new(NewMemberNumber, NewMemberName, NewMemberType, Null(NewMemberUnit), null, null, null, BuildCustomFields(false)), "Yönetici");
        Status(result.Success, result.Message); if (result.Success) await SearchMembersAsync();
    }

    private async Task ArchiveSelectedMemberAsync()
    {
        if (SelectedMember is null) return; var result = await _members.ArchiveAsync(SelectedMember.Id, "Yönetici"); Status(result.Success, result.Message); if (result.Success) await SearchMembersAsync();
    }

    private async Task DeleteSelectedMemberAsync()
    {
        if (SelectedMember is null) return; var result = await _members.DeleteAsync(SelectedMember.Id, "Yönetici"); Status(result.Success, result.Message); if (result.Success) await SearchMembersAsync();
    }

    private async Task ExportReportAsync()
    {
        if (!EnsureAdmin()) return;
        var directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Qylent Kütüphane Raporları");
        var path = Path.Combine(directory, $"kutuphane-raporu-{DateTime.Now:yyyyMMdd-HHmmss}.xlsx");
        var result = await _reports.ExportAsync(new((ReportPeriodKind)Math.Clamp(ReportPeriodIndex, 0, 3), DateOnly.FromDateTime(ReportStart), DateOnly.FromDateTime(ReportEnd),
            SelectedReportColumns(), IncludeOverdueSheet, IncludeReservationsSheet, path));
        Status(result.Success, result.Message + (result.Success ? $" {path}" : string.Empty));
    }

    private async Task SaveSettingsAsync()
    {
        if (!int.TryParse(SettingsLoanDays, out var days) || !int.TryParse(SettingsMaxLoans, out var maxLoans) || !int.TryParse(SettingsMaxRenewals, out var renewals))
        { Status(false, "Ayar sınırlarına geçerli sayılar girin."); return; }
        var result = await _administration.UpdateLibrarySettingsAsync(new(SettingsLibraryName, Null(SettingsLogoPath),
            (LibraryType)Math.Clamp(SettingsLibraryTypeIndex, 0, 3), (OperatorMode)Math.Clamp(SettingsOperatorModeIndex, 0, 2),
            days, maxLoans, renewals, SettingsBackupDirectory));
        Status(result.Success, result.Message);
        if (result.Success) await LoadAdministrationAsync();
    }

    private async Task SaveReportPresetAsync()
    {
        var result = await _administration.SaveReportPresetAsync(new(SelectedReportPreset?.Id, ReportPresetName,
            SelectedReportColumns(), IncludeOverdueSheet, IncludeReservationsSheet));
        Status(result.Success, result.Message);
        if (result.Success) { await LoadReportPresetsAsync(); SelectedReportPreset = ReportPresets.FirstOrDefault(x => x.Id == result.Value?.Id); }
    }

    private void ApplyReportPreset()
    {
        if (SelectedReportPreset is null) return;
        string[] columns;
        try { columns = JsonSerializer.Deserialize<string[]>(SelectedReportPreset.ColumnsJson) ?? []; }
        catch (JsonException) { Status(false, "Rapor şablonunun sütun bilgisi okunamadı."); return; }
        IncludeReportDate = columns.Contains("Tarih", StringComparer.OrdinalIgnoreCase);
        IncludeReportActivity = columns.Contains("İşlem", StringComparer.OrdinalIgnoreCase);
        IncludeReportOperator = columns.Contains("Görevli", StringComparer.OrdinalIgnoreCase);
        IncludeReportEntity = columns.Contains("Kayıt Türü", StringComparer.OrdinalIgnoreCase);
        IncludeReportDescription = columns.Contains("Açıklama", StringComparer.OrdinalIgnoreCase);
        IncludeOverdueSheet = SelectedReportPreset.IncludeOverdueSheet;
        IncludeReservationsSheet = SelectedReportPreset.IncludeReservationsSheet;
        ReportPresetName = SelectedReportPreset.Name;
        OnPropertyChanged(string.Empty);
        Status(true, "Rapor şablonu uygulandı.");
    }

    private async Task DeleteReportPresetAsync()
    {
        if (SelectedReportPreset is null) return;
        var result = await _administration.DeleteReportPresetAsync(SelectedReportPreset.Id);
        Status(result.Success, result.Message);
        if (result.Success) { SelectedReportPreset = null; ReportPresetName = string.Empty; await LoadReportPresetsAsync(); OnPropertyChanged(nameof(ReportPresetName)); }
    }

    private async Task AddRuleAsync()
    {
        if (!int.TryParse(NewRuleDays, out var days) || !int.TryParse(NewRuleMaxLoans, out var maxLoans) || !int.TryParse(NewRuleMaxRenewals, out var renewals)) { Status(false, "Kural sınırlarına geçerli sayılar girin."); return; }
        var result = await _administration.AddLoanRuleAsync(new(NewRuleName, Null(NewRuleMemberType), Null(NewRuleCategory), days, maxLoans, renewals, LoanRules.Count + 1));
        Status(result.Success, result.Message); if (result.Success) await LoadAdministrationAsync();
    }

    private async Task AddOperatorAsync()
    {
        var result = await _administration.AddOperatorAsync(new(NewOperatorName, Null(NewOperatorPin))); Status(result.Success, result.Message);
        if (result.Success) { NewOperatorName = NewOperatorPin = string.Empty; OnPropertyChanged(string.Empty); await LoadAdministrationAsync(); }
    }

    private async Task AddMemberFieldAsync()
    {
        var result = await _administration.AddMemberFieldAsync(new(NewFieldName, (MemberFieldType)Math.Clamp(NewFieldTypeIndex, 0, 4), NewFieldRequired, NewFieldSensitive, Null(NewFieldChoices)));
        Status(result.Success, result.Message); if (result.Success) { NewFieldName = NewFieldChoices = string.Empty; OnPropertyChanged(string.Empty); await LoadAdministrationAsync(); }
    }

    private async Task LoadAdministrationAsync()
    {
        Replace(LoanRules, await _administration.GetLoanRulesAsync()); Replace(Operators, await _administration.GetOperatorsAsync());
        Replace(MemberFields, await _administration.GetMemberFieldsAsync()); Replace(AuditEntries, await _administration.GetAuditAsync());
        await LoadReportPresetsAsync();
        var profile = await _administration.GetLibraryProfileAsync();
        if (profile is not null)
        {
            SettingsLibraryName = profile.Name; SettingsLogoPath = profile.LogoPath ?? string.Empty;
            SettingsLibraryTypeIndex = (int)profile.LibraryType; SettingsOperatorModeIndex = (int)profile.OperatorMode;
            SettingsLoanDays = profile.DefaultLoanDays.ToString(); SettingsMaxLoans = profile.DefaultMaxActiveLoans.ToString();
            SettingsMaxRenewals = profile.DefaultMaxRenewals.ToString(); SettingsBackupDirectory = profile.BackupDirectory;
        }
        var values = EditableMemberFields.ToDictionary(x => x.Id, x => x.Value);
        EditableMemberFields.Clear();
        foreach (var field in MemberFields.Where(x => x.IsEnabled)) EditableMemberFields.Add(new EditableMemberField(field.Id, field.Name, field.FieldType, field.IsRequired, values.GetValueOrDefault(field.Id) ?? string.Empty));
        OnPropertyChanged(string.Empty);
    }

    private async Task LoadReportPresetsAsync() => Replace(ReportPresets, await _administration.GetReportPresetsAsync());
    private IReadOnlyList<string> SelectedReportColumns()
    {
        var columns = new List<string>();
        if (IncludeReportDate) columns.Add("Tarih"); if (IncludeReportActivity) columns.Add("İşlem");
        if (IncludeReportOperator) columns.Add("Görevli"); if (IncludeReportEntity) columns.Add("Kayıt Türü");
        if (IncludeReportDescription) columns.Add("Açıklama");
        return columns;
    }

    private bool EnsureAdmin()
    {
        if (IsAdminUnlocked) return true;
        Status(false, "Yönetici oturumu süresi doldu. Yeniden giriş yapın."); OnPropertyChanged(nameof(IsAdminUnlocked)); return false;
    }

    private void Status(bool success, string message) { StatusIsError = !success; StatusMessage = message; }
    private void RaiseCommands()
    {
        foreach (var command in new[] { ReserveCommand, RenewCommand, AddBookCommand, AddMemberCommand, ExportReportCommand, AddRuleCommand, AddOperatorCommand, AddMemberFieldCommand, SaveSettingsCommand, SaveReportPresetCommand, DeleteReportPresetCommand, UpdateSelectedBookCommand, ArchiveSelectedBookCommand, DeleteSelectedBookCommand, UpdateSelectedMemberCommand, ArchiveSelectedMemberCommand, DeleteSelectedMemberCommand })
            (command as AsyncRelayCommand)?.RaiseCanExecuteChanged();
        foreach (var command in new[] { LoadSelectedBookCommand, LoadSelectedMemberCommand, ApplyReportPresetCommand }) (command as RelayCommand)?.RaiseCanExecuteChanged();
        OnPropertyChanged(nameof(IsAdminUnlocked));
    }
    private static string? Null(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private IReadOnlyDictionary<Guid, string?> BuildCustomFields(bool includeEmpty) => EditableMemberFields
        .Where(x => includeEmpty || !string.IsNullOrWhiteSpace(x.Value)).ToDictionary(x => x.Id, x => (string?)x.Value);
    private static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> source) { target.Clear(); foreach (var item in source) target.Add(item); }
    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null) { if (EqualityComparer<T>.Default.Equals(field, value)) return false; field = value; OnPropertyChanged(name); return true; }
    private void OnPropertyChanged([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
}

public sealed class EditableMemberField(Guid id, string name, MemberFieldType fieldType, bool isRequired, string value)
{
    public Guid Id { get; } = id;
    public string Name { get; } = name;
    public MemberFieldType FieldType { get; } = fieldType;
    public bool IsRequired { get; } = isRequired;
    public string Value { get; set; } = value;
    public string Label => IsRequired ? $"{Name} (zorunlu)" : Name;
}
