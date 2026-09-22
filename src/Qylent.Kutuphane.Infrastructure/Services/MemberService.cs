using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Text.Json;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class MemberService(
    IDbContextFactory<LibraryDbContext> contextFactory,
    ISecurityService securityService) : IMemberService
{
    public async Task<MemberDetails?> GetAsync(Guid memberId, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.Members.AsNoTracking().Include(x => x.CustomFieldValues)
            .SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return null;
        var sensitiveFields = await db.MemberFieldDefinitions.AsNoTracking().Where(x => x.IsSensitive)
            .Select(x => x.Id).ToHashSetAsync(cancellationToken);
        return new MemberDetails(member.Id, member.MemberNumber, member.FullName, member.MemberType, member.ClassOrUnit,
            Decrypt(member.EncryptedPhone), Decrypt(member.EncryptedEmail), Decrypt(member.EncryptedAddress),
            member.CustomFieldValues.ToDictionary(x => x.DefinitionId, x => DecryptField(x.Value, sensitiveFields.Contains(x.DefinitionId))), member.IsArchived);
    }

    public async Task<OperationResult<Member>> AddAsync(MemberInput input, string actor, CancellationToken cancellationToken = default)
    {
        var memberNumber = input.MemberNumber.Trim();
        if (memberNumber.Length < 1) return OperationResult<Member>.Fail("Üye numarası zorunludur.");
        if (string.IsNullOrWhiteSpace(input.FullName)) return OperationResult<Member>.Fail("Ad soyad zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.Members.AnyAsync(x => x.MemberNumber == memberNumber, cancellationToken))
            return OperationResult<Member>.Fail("Bu üye numarası zaten kullanılıyor.");
        var member = new Member
        {
            MemberNumber = memberNumber, FullName = input.FullName.Trim(),
            MemberType = string.IsNullOrWhiteSpace(input.MemberType) ? "Genel" : input.MemberType.Trim(),
            ClassOrUnit = Clean(input.ClassOrUnit),
            EncryptedPhone = Encrypt(input.Phone), EncryptedEmail = Encrypt(input.Email), EncryptedAddress = Encrypt(input.Address)
        };
        db.Members.Add(member);
        var requiredDefinitions = await db.MemberFieldDefinitions.Where(x => x.IsEnabled && x.IsRequired).ToListAsync(cancellationToken);
        foreach (var required in requiredDefinitions)
            if (input.CustomFields is null || !input.CustomFields.TryGetValue(required.Id, out var requiredValue) || string.IsNullOrWhiteSpace(requiredValue))
                return OperationResult<Member>.Fail($"{required.Name} alanı zorunludur.");
        if (input.CustomFields is not null)
        {
            var definitions = await db.MemberFieldDefinitions.Where(x => input.CustomFields.Keys.Contains(x.Id) && x.IsEnabled).ToListAsync(cancellationToken);
            foreach (var definition in definitions)
            {
                input.CustomFields.TryGetValue(definition.Id, out var value);
                if (definition.IsRequired && string.IsNullOrWhiteSpace(value))
                    return OperationResult<Member>.Fail($"{definition.Name} alanı zorunludur.");
                var fieldValidation = ValidateFieldValue(definition, value);
                if (!fieldValidation.Success) return OperationResult<Member>.Fail(fieldValidation.Message);
                member.CustomFieldValues.Add(new MemberFieldValue
                {
                    DefinitionId = definition.Id,
                    Value = definition.IsSensitive && !string.IsNullOrEmpty(value) ? securityService.EncryptSensitive(value) : Clean(value)
                });
            }
        }
        CatalogService.AddAudit(db, ActivityType.MemberCreated, actor, nameof(Member), member.Id,
            new { member.MemberNumber, member.FullName, member.MemberType });
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<Member>.Ok(member, "Üye kaydı eklendi.");
    }

    public async Task<IReadOnlyList<MemberSummary>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.Members.AsNoTracking()
            .Where(x => (includeArchived || !x.IsArchived) &&
                (query == "" || x.MemberNumber.Contains(query) || x.FullName.Contains(query) ||
                 (x.ClassOrUnit != null && x.ClassOrUnit.Contains(query))))
            .OrderBy(x => x.FullName).Take(250)
            .Select(x => new MemberSummary(x.Id, x.MemberNumber, x.FullName, x.MemberType, x.ClassOrUnit, x.IsBlocked, x.IsArchived))
            .ToListAsync(cancellationToken);
    }

    public async Task<OperationResult> UpdateAsync(Guid memberId, MemberInput input, string actor, CancellationToken cancellationToken = default)
    {
        var memberNumber = input.MemberNumber.Trim();
        if (memberNumber.Length < 1 || string.IsNullOrWhiteSpace(input.FullName)) return OperationResult.Fail("Üye numarası ve ad soyad zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.Members.Include(x => x.CustomFieldValues).SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return OperationResult.Fail("Üye bulunamadı.");
        if (await db.Members.AnyAsync(x => x.Id != memberId && x.MemberNumber == memberNumber, cancellationToken)) return OperationResult.Fail("Bu üye numarası zaten kullanılıyor.");
        var old = new { member.MemberNumber, member.FullName, member.MemberType, member.ClassOrUnit };
        member.MemberNumber = memberNumber; member.FullName = input.FullName.Trim(); member.MemberType = string.IsNullOrWhiteSpace(input.MemberType) ? "Genel" : input.MemberType.Trim(); member.ClassOrUnit = Clean(input.ClassOrUnit);
        if (input.Phone is not null) member.EncryptedPhone = Encrypt(input.Phone); if (input.Email is not null) member.EncryptedEmail = Encrypt(input.Email); if (input.Address is not null) member.EncryptedAddress = Encrypt(input.Address);
        if (input.CustomFields is not null)
        {
            var definitions = await db.MemberFieldDefinitions.Where(x => input.CustomFields.Keys.Contains(x.Id) && x.IsEnabled).ToListAsync(cancellationToken);
            foreach (var definition in definitions)
            {
                input.CustomFields.TryGetValue(definition.Id, out var value);
                if (definition.IsRequired && string.IsNullOrWhiteSpace(value)) return OperationResult.Fail($"{definition.Name} alanı zorunludur.");
                var fieldValidation = ValidateFieldValue(definition, value);
                if (!fieldValidation.Success) return fieldValidation;
                var stored = member.CustomFieldValues.SingleOrDefault(x => x.DefinitionId == definition.Id);
                var protectedValue = definition.IsSensitive && !string.IsNullOrEmpty(value) ? securityService.EncryptSensitive(value) : Clean(value);
                if (stored is null) member.CustomFieldValues.Add(new MemberFieldValue { DefinitionId = definition.Id, Value = protectedValue }); else stored.Value = protectedValue;
            }
        }
        db.AuditEntries.Add(new AuditEntry { ActivityType = ActivityType.MemberUpdated, ActorName = actor, EntityType = nameof(Member), EntityId = member.Id, OldStateJson = System.Text.Json.JsonSerializer.Serialize(old), NewStateJson = System.Text.Json.JsonSerializer.Serialize(new { member.MemberNumber, member.FullName, member.MemberType, member.ClassOrUnit }) });
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Üye bilgileri güncellendi.");
    }

    public async Task<OperationResult> ArchiveAsync(Guid memberId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.Members.SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return OperationResult.Fail("Üye bulunamadı.");
        if (await db.Loans.AnyAsync(x => x.MemberId == memberId && x.Status == LoanStatus.Active, cancellationToken))
            return OperationResult.Fail("Aktif ödüncü bulunan üye arşivlenemez.");
        if (await db.Reservations.AnyAsync(x => x.MemberId == memberId && (x.Status == ReservationStatus.Waiting || x.Status == ReservationStatus.Ready), cancellationToken))
            return OperationResult.Fail("Aktif ayırtması bulunan üye arşivlenemez.");
        member.IsArchived = true;
        CatalogService.AddAudit(db, ActivityType.MemberArchived, actor, nameof(Member), member.Id,
            new { member.MemberNumber, member.FullName });
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Üye arşivlendi.");
    }

    public async Task<OperationResult> DeleteAsync(Guid memberId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var member = await db.Members.Include(x => x.CustomFieldValues).SingleOrDefaultAsync(x => x.Id == memberId, cancellationToken);
        if (member is null) return OperationResult.Fail("Üye bulunamadı.");
        if (await db.Loans.AnyAsync(x => x.MemberId == memberId, cancellationToken) || await db.Reservations.AnyAsync(x => x.MemberId == memberId, cancellationToken))
            return OperationResult.Fail("Geçmişi bulunan üye kalıcı silinemez; arşivleyin.");
        CatalogService.AddAudit(db, ActivityType.MemberArchived, actor, nameof(Member), member.Id, new { member.MemberNumber, member.FullName }, "İlişkisiz hatalı kayıt kalıcı silindi.");
        db.Members.Remove(member); await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("İlişkisiz üye kaydı kalıcı silindi.");
    }

    private string? Encrypt(string? value) => string.IsNullOrWhiteSpace(value) ? null : securityService.EncryptSensitive(value.Trim());
    private string? Decrypt(string? value) => string.IsNullOrWhiteSpace(value) ? null : securityService.DecryptSensitive(value);
    private string? DecryptField(string? value, bool isSensitive)
        => string.IsNullOrWhiteSpace(value) || !isSensitive ? value : securityService.DecryptSensitive(value);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static OperationResult ValidateFieldValue(MemberFieldDefinition definition, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return OperationResult.Ok();
        var valid = definition.FieldType switch
        {
            MemberFieldType.Number => decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("tr-TR"), out _),
            MemberFieldType.Date => DateOnly.TryParse(value, CultureInfo.GetCultureInfo("tr-TR"), DateTimeStyles.None, out _),
            MemberFieldType.Boolean => bool.TryParse(value, out _) || value.Equals("evet", StringComparison.OrdinalIgnoreCase) || value.Equals("hayır", StringComparison.OrdinalIgnoreCase),
            MemberFieldType.Choice => JsonSerializer.Deserialize<string[]>(definition.ChoiceOptionsJson ?? "[]")?.Contains(value, StringComparer.OrdinalIgnoreCase) == true,
            _ => true
        };
        return valid ? OperationResult.Ok() : OperationResult.Fail($"{definition.Name} alanındaki değer {definition.FieldType} türüne uygun değil.");
    }
}
