using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class SetupService(IDbContextFactory<LibraryDbContext> contextFactory) : ISetupService
{
    public async Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        return await db.LibraryProfiles.AnyAsync(x => x.SetupCompleted, cancellationToken);
    }

    public async Task<OperationResult> CompleteSetupAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        var validation = Validate(request);
        if (!validation.Success) return validation;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (await db.LibraryProfiles.AnyAsync(x => x.SetupCompleted, cancellationToken))
            return OperationResult.Fail("İlk kurulum daha önce tamamlanmış.");

        Directory.CreateDirectory(request.BackupDirectory);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var profile = new LibraryProfile
        {
            Name = request.LibraryName.Trim(),
            LogoPath = string.IsNullOrWhiteSpace(request.LogoPath) ? null : request.LogoPath,
            LibraryType = request.LibraryType,
            OperatorMode = request.OperatorMode,
            DefaultLoanDays = request.DefaultLoanDays,
            DefaultMaxActiveLoans = request.DefaultMaxActiveLoans,
            DefaultMaxRenewals = request.DefaultMaxRenewals,
            BackupDirectory = request.BackupDirectory,
            SetupCompleted = true
        };
        db.LibraryProfiles.Add(profile);
        var password = PasswordHasher.Hash(request.AdminPassword);
        db.AdminCredentials.Add(new AdminCredential { PasswordHash = password.Hash, PasswordSalt = password.Salt });
        for (var i = 0; i < request.SecurityQuestions.Count; i++)
        {
            var item = request.SecurityQuestions[i];
            var answer = PasswordHasher.Hash(PasswordHasher.NormalizeAnswer(item.Answer));
            db.SecurityAnswers.Add(new SecurityAnswer
            {
                Order = i + 1,
                Question = item.Question.Trim(),
                AnswerHash = answer.Hash,
                AnswerSalt = answer.Salt
            });
        }

        db.Operators.Add(new Operator { Name = "Ortak Görevli" });
        foreach (var definition in CreateProfileFields(request.LibraryType)) db.MemberFieldDefinitions.Add(definition);
        db.AuditEntries.Add(new AuditEntry
        {
            ActivityType = ActivityType.SetupCompleted,
            ActorName = "Kurulum",
            EntityType = nameof(LibraryProfile),
            EntityId = profile.Id,
            Description = "İlk kurulum tamamlandı."
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("İlk kurulum tamamlandı.");
    }

    private static OperationResult Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.LibraryName)) return OperationResult.Fail("Kütüphane adı zorunludur.");
        if (request.AdminPassword.Length < 10) return OperationResult.Fail("Yönetici şifresi en az 10 karakter olmalıdır.");
        if (request.SecurityQuestions.Count != 3 || request.SecurityQuestions.Any(x => string.IsNullOrWhiteSpace(x.Question) || string.IsNullOrWhiteSpace(x.Answer)))
            return OperationResult.Fail("Üç güvenlik sorusu ve cevabı zorunludur.");
        if (request.DefaultLoanDays is < 1 or > 365) return OperationResult.Fail("Ödünç süresi 1 ile 365 gün arasında olmalıdır.");
        if (request.DefaultMaxActiveLoans is < 1 or > 100) return OperationResult.Fail("Aktif kitap sınırı 1 ile 100 arasında olmalıdır.");
        if (request.DefaultMaxRenewals is < 0 or > 20) return OperationResult.Fail("Uzatma sınırı 0 ile 20 arasında olmalıdır.");
        if (string.IsNullOrWhiteSpace(request.BackupDirectory)) return OperationResult.Fail("Yedekleme klasörü zorunludur.");
        return OperationResult.Ok();
    }

    private static IEnumerable<MemberFieldDefinition> CreateProfileFields(LibraryType type)
    {
        if (type == LibraryType.School)
        {
            yield return new() { Name = "Sınıf", FieldType = MemberFieldType.Text, DisplayOrder = 10, IsEnabled = true };
            yield return new() { Name = "Veli adı", FieldType = MemberFieldType.Text, DisplayOrder = 20, IsSensitive = true, IsEnabled = true };
            yield return new() { Name = "Veli telefonu", FieldType = MemberFieldType.Text, DisplayOrder = 30, IsSensitive = true, IsEnabled = true };
        }
        else if (type == LibraryType.Public)
        {
            yield return new() { Name = "Doğum tarihi", FieldType = MemberFieldType.Date, DisplayOrder = 10, IsSensitive = true, IsEnabled = true };
            yield return new() { Name = "Üyelik türü", FieldType = MemberFieldType.Choice, ChoiceOptionsJson = "[\"Standart\",\"Çocuk\",\"Öğrenci\"]", DisplayOrder = 20, IsEnabled = true };
        }
        else if (type == LibraryType.PrivateInstitution)
        {
            yield return new() { Name = "Birim", FieldType = MemberFieldType.Text, DisplayOrder = 10, IsEnabled = true };
            yield return new() { Name = "Sicil numarası", FieldType = MemberFieldType.Text, DisplayOrder = 20, IsSensitive = true, IsEnabled = true };
        }
    }
}

