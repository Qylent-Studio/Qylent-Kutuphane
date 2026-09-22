using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class AdministrationService(IDbContextFactory<LibraryDbContext> contextFactory) : IAdministrationService
{
    private static readonly string[] ReportColumns = ["Tarih", "İşlem", "Görevli", "Kayıt Türü", "Açıklama"];

    public async Task<LibraryProfile?> GetLibraryProfileAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.LibraryProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.SetupCompleted, cancellationToken);
    }

    public async Task<OperationResult> UpdateLibrarySettingsAsync(LibrarySettingsInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.LibraryName)) return OperationResult.Fail("Kütüphane adı zorunludur.");
        if (input.DefaultLoanDays is < 1 or > 365 || input.DefaultMaxActiveLoans is < 1 or > 100 || input.DefaultMaxRenewals is < 0 or > 20)
            return OperationResult.Fail("Ödünç ayarları geçerli sınırlarda olmalıdır.");
        if (string.IsNullOrWhiteSpace(input.BackupDirectory)) return OperationResult.Fail("Yedekleme klasörü zorunludur.");

        string backupDirectory;
        try { backupDirectory = Path.GetFullPath(input.BackupDirectory.Trim()); }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        { return OperationResult.Fail($"Yedekleme klasörü geçersiz: {exception.Message}"); }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (input.OperatorMode != OperatorMode.Shared)
        {
            var activeOperators = db.Operators.Where(x => x.IsActive);
            if (!await activeOperators.AnyAsync(cancellationToken))
                return OperationResult.Fail("Bu görevli modu için en az bir aktif görevli ekleyin.");
            if (input.OperatorMode == OperatorMode.Pin && !await activeOperators.AnyAsync(x => x.PinHash != null && x.PinSalt != null, cancellationToken))
                return OperationResult.Fail("PIN modu için en az bir aktif görevliye PIN tanımlayın.");
        }
        var profile = await db.LibraryProfiles.SingleOrDefaultAsync(x => x.SetupCompleted, cancellationToken);
        if (profile is null) return OperationResult.Fail("Tamamlanmış kurulum profili bulunamadı.");
        var previousType = profile.LibraryType;
        await SynchronizeProfileFieldsAsync(db, input.LibraryType, cancellationToken);
        profile.Name = input.LibraryName.Trim();
        profile.LogoPath = Clean(input.LogoPath);
        profile.LibraryType = input.LibraryType;
        profile.ThemePreference = input.ThemePreference;
        profile.OperatorMode = input.OperatorMode;
        profile.DefaultLoanDays = input.DefaultLoanDays;
        profile.DefaultMaxActiveLoans = input.DefaultMaxActiveLoans;
        profile.DefaultMaxRenewals = input.DefaultMaxRenewals;
        profile.BackupDirectory = backupDirectory;
        CatalogService.AddAudit(db, ActivityType.SettingsUpdated, "Yönetici", nameof(LibraryProfile), profile.Id,
            new { profile.Name, PreviousLibraryType = previousType, profile.LibraryType, profile.ThemePreference, profile.OperatorMode, profile.DefaultLoanDays, profile.DefaultMaxActiveLoans, profile.DefaultMaxRenewals, profile.BackupDirectory }, "Kütüphane ayarları ve kurum profili güncellendi.");
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok(previousType == input.LibraryType
            ? "Ayarlar kaydedildi."
            : $"Kurum profili {LibraryProfileRules.DisplayName(input.LibraryType)} olarak veri kaybı olmadan güncellendi.");
    }

    public async Task<IReadOnlyList<LoanRule>> GetLoanRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.LoanRules.AsNoTracking().OrderByDescending(x => x.Priority).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddLoanRuleAsync(LoanRuleInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateLoanRule(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rule = new LoanRule { Name = input.Name.Trim(), MemberType = Clean(input.MemberType), BookCategory = Clean(input.BookCategory), LoanDays = input.LoanDays, MaxActiveLoans = input.MaxActiveLoans, MaxRenewals = input.MaxRenewals, Priority = input.Priority };
        db.LoanRules.Add(rule); CatalogService.AddAudit(db, ActivityType.RuleCreated, "Yönetici", nameof(LoanRule), rule.Id, rule, "Ödünç kuralı eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Ödünç kuralı eklendi.");
    }

    public async Task<OperationResult> UpdateLoanRuleAsync(Guid id, LoanRuleInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateLoanRule(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.LoanRules.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null) return OperationResult.Fail("Ödünç kuralı bulunamadı.");
        var old = new { rule.Name, rule.MemberType, rule.BookCategory, rule.LoanDays, rule.MaxActiveLoans, rule.MaxRenewals, rule.Priority, rule.IsActive };
        rule.Name = input.Name.Trim(); rule.MemberType = Clean(input.MemberType); rule.BookCategory = Clean(input.BookCategory);
        rule.LoanDays = input.LoanDays; rule.MaxActiveLoans = input.MaxActiveLoans; rule.MaxRenewals = input.MaxRenewals; rule.Priority = input.Priority;
        CatalogService.AddAudit(db, ActivityType.RuleUpdated, "Yönetici", nameof(LoanRule), rule.Id, old, "Ödünç kuralı güncellendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Ödünç kuralı güncellendi.");
    }

    public async Task<OperationResult> SetLoanRuleActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.LoanRules.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null) return OperationResult.Fail("Ödünç kuralı bulunamadı.");
        rule.IsActive = isActive;
        CatalogService.AddAudit(db, ActivityType.RuleUpdated, "Yönetici", nameof(LoanRule), rule.Id, new { rule.Name, IsActive = isActive }, isActive ? "Ödünç kuralı etkinleştirildi." : "Ödünç kuralı devre dışı bırakıldı.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok(isActive ? "Ödünç kuralı etkinleştirildi." : "Ödünç kuralı devre dışı bırakıldı.");
    }

    public async Task<OperationResult> DeleteLoanRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rule = await db.LoanRules.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (rule is null) return OperationResult.Fail("Ödünç kuralı bulunamadı.");
        db.LoanRules.Remove(rule); CatalogService.AddAudit(db, ActivityType.RuleDeleted, "Yönetici", nameof(LoanRule), id, new { rule.Name }, "Ödünç kuralı silindi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Ödünç kuralı silindi.");
    }

    public async Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Operators.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddOperatorAsync(OperatorInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateOperator(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Operators.AnyAsync(x => x.Name == input.Name.Trim(), cancellationToken)) return OperationResult.Fail("Bu görevli adı zaten kullanılıyor.");
        var item = new Operator { Name = input.Name.Trim() };
        if (!string.IsNullOrEmpty(input.Pin)) { var hash = PasswordHasher.Hash(input.Pin); item.PinHash = hash.Hash; item.PinSalt = hash.Salt; }
        db.Operators.Add(item); CatalogService.AddAudit(db, ActivityType.OperatorCreated, "Yönetici", nameof(Operator), item.Id, new { item.Name }, "Görevli eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Görevli eklendi.");
    }

    public async Task<OperationResult> UpdateOperatorAsync(Guid id, OperatorInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateOperator(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Operators.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Görevli bulunamadı.");
        var name = input.Name.Trim();
        if (await db.Operators.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken)) return OperationResult.Fail("Bu görevli adı zaten kullanılıyor.");
        var oldName = item.Name; item.Name = name;
        if (!string.IsNullOrEmpty(input.Pin)) { var hash = PasswordHasher.Hash(input.Pin); item.PinHash = hash.Hash; item.PinSalt = hash.Salt; }
        CatalogService.AddAudit(db, ActivityType.OperatorUpdated, "Yönetici", nameof(Operator), item.Id, new { OldName = oldName, item.Name }, "Görevli güncellendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Görevli güncellendi.");
    }

    public async Task<OperationResult> SetOperatorActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Operators.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Görevli bulunamadı.");
        if (!isActive)
        {
            var profile = await db.LibraryProfiles.SingleOrDefaultAsync(x => x.SetupCompleted, cancellationToken);
            if (profile?.OperatorMode == OperatorMode.NameSelection && !await db.Operators.AnyAsync(x => x.Id != id && x.IsActive, cancellationToken))
                return OperationResult.Fail("İsim seçimi modunda son aktif görevli devre dışı bırakılamaz.");
            if (profile?.OperatorMode == OperatorMode.Pin && !await db.Operators.AnyAsync(x => x.Id != id && x.IsActive && x.PinHash != null && x.PinSalt != null, cancellationToken))
                return OperationResult.Fail("PIN modunda son aktif PIN'li görevli devre dışı bırakılamaz.");
        }
        item.IsActive = isActive;
        CatalogService.AddAudit(db, ActivityType.OperatorUpdated, "Yönetici", nameof(Operator), item.Id, new { item.Name, IsActive = isActive }, isActive ? "Görevli etkinleştirildi." : "Görevli devre dışı bırakıldı.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok(isActive ? "Görevli etkinleştirildi." : "Görevli devre dışı bırakıldı.");
    }

    public async Task<OperationResult> DeleteOperatorAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Operators.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Görevli bulunamadı.");
        if (await db.Loans.AnyAsync(x => x.OperatorId == id, cancellationToken) || await db.AuditEntries.AnyAsync(x => x.OperatorId == id, cancellationToken))
            return OperationResult.Fail("İşlem geçmişi bulunan görevli kalıcı silinemez; devre dışı bırakın.");
        var profile = await db.LibraryProfiles.SingleOrDefaultAsync(x => x.SetupCompleted, cancellationToken);
        if (item.IsActive && profile?.OperatorMode == OperatorMode.NameSelection && !await db.Operators.AnyAsync(x => x.Id != id && x.IsActive, cancellationToken))
            return OperationResult.Fail("İsim seçimi modunda son aktif görevli silinemez.");
        if (item.IsActive && profile?.OperatorMode == OperatorMode.Pin && !await db.Operators.AnyAsync(x => x.Id != id && x.IsActive && x.PinHash != null && x.PinSalt != null, cancellationToken))
            return OperationResult.Fail("PIN modunda son aktif PIN'li görevli silinemez.");
        db.Operators.Remove(item); CatalogService.AddAudit(db, ActivityType.OperatorDeleted, "Yönetici", nameof(Operator), id, new { item.Name }, "İlişkisiz görevli silindi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("İlişkisiz görevli silindi.");
    }

    public async Task<IReadOnlyList<MemberFieldDefinition>> GetMemberFieldsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.MemberFieldDefinitions.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddMemberFieldAsync(MemberFieldInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMemberField(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.MemberFieldDefinitions.AnyAsync(x => x.Name == input.Name.Trim(), cancellationToken)) return OperationResult.Fail("Bu alan adı zaten kullanılıyor.");
        var order = (await db.MemberFieldDefinitions.MaxAsync(x => (int?)x.DisplayOrder, cancellationToken) ?? 0) + 10;
        var choices = ChoiceOptions(input);
        var item = new MemberFieldDefinition { Name = input.Name.Trim(), FieldType = input.FieldType, IsRequired = input.IsRequired, IsSensitive = input.IsSensitive, ChoiceOptionsJson = choices, DisplayOrder = order };
        db.MemberFieldDefinitions.Add(item); CatalogService.AddAudit(db, ActivityType.MemberFieldCreated, "Yönetici", nameof(MemberFieldDefinition), item.Id, item, "Üye alanı eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Üye alanı eklendi.");
    }

    public async Task<OperationResult> UpdateMemberFieldAsync(Guid id, MemberFieldInput input, CancellationToken cancellationToken = default)
    {
        var validation = ValidateMemberField(input); if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.MemberFieldDefinitions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Üye alanı bulunamadı.");
        var name = input.Name.Trim();
        if (await db.MemberFieldDefinitions.AnyAsync(x => x.Id != id && x.Name == name, cancellationToken)) return OperationResult.Fail("Bu alan adı zaten kullanılıyor.");
        var hasValues = await db.MemberFieldValues.AnyAsync(x => x.DefinitionId == id, cancellationToken);
        var choices = ChoiceOptions(input);
        if (hasValues && (item.FieldType != input.FieldType || item.IsSensitive != input.IsSensitive || item.ChoiceOptionsJson != choices))
            return OperationResult.Fail("Değeri bulunan alanın türü, şifreleme ayarı veya seçenekleri değiştirilemez.");
        if (hasValues && input.IsRequired && !item.IsRequired)
        {
            var memberCount = await db.Members.CountAsync(cancellationToken);
            var valueCount = await db.MemberFieldValues.CountAsync(x => x.DefinitionId == id && x.Value != null && x.Value != "", cancellationToken);
            if (valueCount < memberCount) return OperationResult.Fail("Eksik değerler varken alan zorunlu yapılamaz.");
        }
        var old = new { item.Name, item.FieldType, item.IsRequired, item.IsSensitive, item.ChoiceOptionsJson };
        item.Name = name; item.FieldType = input.FieldType; item.IsRequired = input.IsRequired; item.IsSensitive = input.IsSensitive; item.ChoiceOptionsJson = choices;
        CatalogService.AddAudit(db, ActivityType.MemberFieldUpdated, "Yönetici", nameof(MemberFieldDefinition), item.Id, old, "Üye alanı güncellendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Üye alanı güncellendi.");
    }

    public async Task<OperationResult> SetMemberFieldEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.MemberFieldDefinitions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Üye alanı bulunamadı.");
        item.IsEnabled = isEnabled;
        CatalogService.AddAudit(db, ActivityType.MemberFieldUpdated, "Yönetici", nameof(MemberFieldDefinition), item.Id, new { item.Name, IsEnabled = isEnabled }, isEnabled ? "Üye alanı etkinleştirildi." : "Üye alanı devre dışı bırakıldı.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok(isEnabled ? "Üye alanı etkinleştirildi." : "Üye alanı devre dışı bırakıldı.");
    }

    public async Task<OperationResult> DeleteMemberFieldAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.MemberFieldDefinitions.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (item is null) return OperationResult.Fail("Üye alanı bulunamadı.");
        if (await db.MemberFieldValues.AnyAsync(x => x.DefinitionId == id, cancellationToken)) return OperationResult.Fail("Değeri bulunan üye alanı kalıcı silinemez; devre dışı bırakın.");
        db.MemberFieldDefinitions.Remove(item); CatalogService.AddAudit(db, ActivityType.MemberFieldDeleted, "Yönetici", nameof(MemberFieldDefinition), id, new { item.Name }, "Kullanılmayan üye alanı silindi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Kullanılmayan üye alanı silindi.");
    }

    public async Task<IReadOnlyList<ReportPreset>> GetReportPresetsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.ReportPresets.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult<ReportPreset>> SaveReportPresetAsync(ReportPresetInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return OperationResult<ReportPreset>.Fail("Şablon adı zorunludur.");
        var columns = input.Columns.Where(x => ReportColumns.Contains(x, StringComparer.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (columns.Length == 0) return OperationResult<ReportPreset>.Fail("En az bir rapor sütunu seçin.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        ReportPreset preset;
        if (input.Id is Guid id)
        {
            preset = await db.ReportPresets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken)
                ?? new ReportPreset { Id = id };
            if (db.Entry(preset).State == EntityState.Detached) db.ReportPresets.Add(preset);
        }
        else
        {
            preset = await db.ReportPresets.SingleOrDefaultAsync(x => x.Name == input.Name.Trim(), cancellationToken)
                ?? new ReportPreset();
            if (db.Entry(preset).State == EntityState.Detached) db.ReportPresets.Add(preset);
        }
        preset.Name = input.Name.Trim();
        preset.ColumnsJson = JsonSerializer.Serialize(columns);
        preset.IncludeOverdueSheet = input.IncludeOverdueSheet;
        preset.IncludeReservationsSheet = input.IncludeReservationsSheet;
        CatalogService.AddAudit(db, ActivityType.ReportPresetSaved, "Yönetici", nameof(ReportPreset), preset.Id,
            new { preset.Name, Columns = columns, preset.IncludeOverdueSheet, preset.IncludeReservationsSheet }, "Rapor şablonu kaydedildi.");
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<ReportPreset>.Ok(preset, "Rapor şablonu kaydedildi.");
    }

    public async Task<OperationResult> DeleteReportPresetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var preset = await db.ReportPresets.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (preset is null) return OperationResult.Fail("Rapor şablonu bulunamadı.");
        var oldState = new { preset.Name, preset.ColumnsJson, preset.IncludeOverdueSheet, preset.IncludeReservationsSheet };
        db.ReportPresets.Remove(preset);
        CatalogService.AddAudit(db, ActivityType.ReportPresetDeleted, "Yönetici", nameof(ReportPreset), id, oldState, "Rapor şablonu silindi.");
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Rapor şablonu silindi.");
    }

    public async Task<IReadOnlyList<AuditSummary>> GetAuditAsync(int take = 500, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.AuditEntries.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(Math.Clamp(take, 1, 5000))
            .Select(x => new AuditSummary(x.OccurredAtUtc, x.ActivityType.ToString(), x.ActorName, x.EntityType, x.Description)).ToListAsync(cancellationToken);
    }

    private static OperationResult ValidateLoanRule(LoanRuleInput input) => string.IsNullOrWhiteSpace(input.Name) || input.LoanDays is < 1 or > 365 || input.MaxActiveLoans is < 1 or > 100 || input.MaxRenewals is < 0 or > 20
        ? OperationResult.Fail("Kural adı ve sayısal sınırlar geçerli olmalıdır.") : OperationResult.Ok();
    private static OperationResult ValidateOperator(OperatorInput input) => string.IsNullOrWhiteSpace(input.Name)
        ? OperationResult.Fail("Görevli adı zorunludur.")
        : !string.IsNullOrEmpty(input.Pin) && (input.Pin.Length < 4 || input.Pin.Any(x => !char.IsDigit(x)))
            ? OperationResult.Fail("PIN en az dört rakam olmalıdır.") : OperationResult.Ok();
    private static OperationResult ValidateMemberField(MemberFieldInput input) => string.IsNullOrWhiteSpace(input.Name)
        ? OperationResult.Fail("Alan adı zorunludur.")
        : input.FieldType == MemberFieldType.Choice && ChoiceOptions(input) is null
            ? OperationResult.Fail("Seçim alanı için en az bir seçenek girin.") : OperationResult.Ok();
    private static string? ChoiceOptions(MemberFieldInput input) => input.FieldType == MemberFieldType.Choice && !string.IsNullOrWhiteSpace(input.ChoiceOptions)
        ? JsonSerializer.Serialize(input.ChoiceOptions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).Distinct(StringComparer.OrdinalIgnoreCase)) : null;
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static async Task SynchronizeProfileFieldsAsync(LibraryDbContext db, LibraryType targetType, CancellationToken cancellationToken)
    {
        var definitions = await db.MemberFieldDefinitions.ToListAsync(cancellationToken);

        foreach (var legacy in definitions.Where(x => x.ProfileKey is LibraryProfileRules.LegacyClassKey or LibraryProfileRules.LegacyUnitKey))
        {
            if (!legacy.IsSensitive)
            {
                var values = await db.MemberFieldValues.Where(x => x.DefinitionId == legacy.Id && x.Value != null && x.Value != "")
                    .ToListAsync(cancellationToken);
                if (values.Count > 0)
                {
                    var members = await db.Members.Where(x => values.Select(v => v.MemberId).Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
                    foreach (var value in values)
                    {
                        if (members.TryGetValue(value.MemberId, out var member) && string.IsNullOrWhiteSpace(member.ClassOrUnit))
                            member.ClassOrUnit = value.Value;
                    }
                }
            }
            legacy.IsEnabled = false;
        }

        var desired = LibraryProfileRules.Fields(targetType);
        var desiredKeys = desired.Select(x => x.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var managed in definitions.Where(x => x.ProfileKey?.StartsWith("profile.", StringComparison.Ordinal) == true
                                                        && x.ProfileKey is not LibraryProfileRules.LegacyClassKey
                                                        && x.ProfileKey is not LibraryProfileRules.LegacyUnitKey))
            managed.IsEnabled = desiredKeys.Contains(managed.ProfileKey!);

        var order = definitions.Count == 0 ? 0 : definitions.Max(x => x.DisplayOrder);
        foreach (var template in desired)
        {
            var existing = definitions.SingleOrDefault(x => x.ProfileKey == template.Key);
            if (existing is not null)
            {
                existing.IsEnabled = true;
                continue;
            }

            order += 10;
            var field = new MemberFieldDefinition
            {
                Name = template.Name,
                FieldType = template.FieldType,
                IsSensitive = template.IsSensitive,
                ChoiceOptionsJson = template.ChoiceOptionsJson,
                ProfileKey = template.Key,
                DisplayOrder = order,
                IsEnabled = true
            };
            db.MemberFieldDefinitions.Add(field);
            definitions.Add(field);
        }
    }
}
