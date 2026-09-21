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
        var profile = await db.LibraryProfiles.SingleOrDefaultAsync(x => x.SetupCompleted, cancellationToken);
        if (profile is null) return OperationResult.Fail("Tamamlanmış kurulum profili bulunamadı.");
        profile.Name = input.LibraryName.Trim();
        profile.LogoPath = Clean(input.LogoPath);
        profile.LibraryType = input.LibraryType;
        profile.OperatorMode = input.OperatorMode;
        profile.DefaultLoanDays = input.DefaultLoanDays;
        profile.DefaultMaxActiveLoans = input.DefaultMaxActiveLoans;
        profile.DefaultMaxRenewals = input.DefaultMaxRenewals;
        profile.BackupDirectory = backupDirectory;
        CatalogService.AddAudit(db, ActivityType.SettingsUpdated, "Yönetici", nameof(LibraryProfile), profile.Id,
            new { profile.Name, profile.LibraryType, profile.OperatorMode, profile.DefaultLoanDays, profile.DefaultMaxActiveLoans, profile.DefaultMaxRenewals, profile.BackupDirectory }, "Kütüphane ayarları güncellendi.");
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Ayarlar kaydedildi.");
    }

    public async Task<IReadOnlyList<LoanRule>> GetLoanRulesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.LoanRules.AsNoTracking().OrderByDescending(x => x.Priority).ThenBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddLoanRuleAsync(LoanRuleInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name) || input.LoanDays is < 1 or > 365 || input.MaxActiveLoans is < 1 or > 100 || input.MaxRenewals is < 0 or > 20)
            return OperationResult.Fail("Kural adı ve sayısal sınırlar geçerli olmalıdır.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var rule = new LoanRule { Name = input.Name.Trim(), MemberType = Clean(input.MemberType), BookCategory = Clean(input.BookCategory), LoanDays = input.LoanDays, MaxActiveLoans = input.MaxActiveLoans, MaxRenewals = input.MaxRenewals, Priority = input.Priority };
        db.LoanRules.Add(rule); CatalogService.AddAudit(db, ActivityType.RuleCreated, "Yönetici", nameof(LoanRule), rule.Id, rule, "Ödünç kuralı eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Ödünç kuralı eklendi.");
    }

    public async Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Operators.AsNoTracking().OrderBy(x => x.Name).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddOperatorAsync(OperatorInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return OperationResult.Fail("Görevli adı zorunludur.");
        if (!string.IsNullOrEmpty(input.Pin) && (input.Pin.Length < 4 || input.Pin.Any(x => !char.IsDigit(x)))) return OperationResult.Fail("PIN en az dört rakam olmalıdır.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Operators.AnyAsync(x => x.Name == input.Name.Trim(), cancellationToken)) return OperationResult.Fail("Bu görevli adı zaten kullanılıyor.");
        var item = new Operator { Name = input.Name.Trim() };
        if (!string.IsNullOrEmpty(input.Pin)) { var hash = PasswordHasher.Hash(input.Pin); item.PinHash = hash.Hash; item.PinSalt = hash.Salt; }
        db.Operators.Add(item); CatalogService.AddAudit(db, ActivityType.OperatorCreated, "Yönetici", nameof(Operator), item.Id, new { item.Name }, "Görevli eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Görevli eklendi.");
    }

    public async Task<IReadOnlyList<MemberFieldDefinition>> GetMemberFieldsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.MemberFieldDefinitions.AsNoTracking().OrderBy(x => x.DisplayOrder).ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> AddMemberFieldAsync(MemberFieldInput input, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Name)) return OperationResult.Fail("Alan adı zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.MemberFieldDefinitions.AnyAsync(x => x.Name == input.Name.Trim(), cancellationToken)) return OperationResult.Fail("Bu alan adı zaten kullanılıyor.");
        var order = (await db.MemberFieldDefinitions.MaxAsync(x => (int?)x.DisplayOrder, cancellationToken) ?? 0) + 10;
        var choices = input.FieldType == MemberFieldType.Choice && !string.IsNullOrWhiteSpace(input.ChoiceOptions)
            ? JsonSerializer.Serialize(input.ChoiceOptions.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)) : null;
        var item = new MemberFieldDefinition { Name = input.Name.Trim(), FieldType = input.FieldType, IsRequired = input.IsRequired, IsSensitive = input.IsSensitive, ChoiceOptionsJson = choices, DisplayOrder = order };
        db.MemberFieldDefinitions.Add(item); CatalogService.AddAudit(db, ActivityType.MemberFieldCreated, "Yönetici", nameof(MemberFieldDefinition), item.Id, item, "Üye alanı eklendi.");
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Üye alanı eklendi.");
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

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
