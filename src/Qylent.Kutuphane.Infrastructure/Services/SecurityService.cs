using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class SecurityService(
    IDbContextFactory<LibraryDbContext> contextFactory,
    SensitiveDataProtector protector) : ISecurityService
{
    private static readonly TimeSpan SessionLength = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan LockLength = TimeSpan.FromSeconds(30);

    public async Task<OperationResult<AdminSession>> LoginAdminAsync(string password, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var credential = await db.AdminCredentials.SingleOrDefaultAsync(cancellationToken);
        if (credential is null) return OperationResult<AdminSession>.Fail("Yönetici hesabı henüz oluşturulmamış.");
        var now = DateTimeOffset.UtcNow;
        if (credential.LockedUntilUtc > now)
            return OperationResult<AdminSession>.Fail("Çok fazla hatalı deneme yapıldı. Biraz sonra tekrar deneyin.");

        if (!PasswordHasher.Verify(password, credential.PasswordHash, credential.PasswordSalt))
        {
            credential.FailedAttempts++;
            if (credential.FailedAttempts >= 5)
            {
                credential.FailedAttempts = 0;
                credential.LockedUntilUtc = now.Add(LockLength);
            }
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<AdminSession>.Fail("Yönetici şifresi hatalı.");
        }

        credential.FailedAttempts = 0;
        credential.LockedUntilUtc = null;
        credential.LastLoginAtUtc = now;
        db.AuditEntries.Add(new AuditEntry
        {
            ActivityType = ActivityType.AdminLogin,
            ActorName = "Yönetici",
            EntityType = nameof(AdminCredential),
            EntityId = credential.Id,
            Description = "Yönetici oturumu açıldı."
        });
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<AdminSession>.Ok(new AdminSession(Guid.NewGuid(), now.Add(SessionLength)));
    }

    public bool IsAdminSessionValid(AdminSession? session) => session is not null && session.ExpiresAtUtc > DateTimeOffset.UtcNow;

    public async Task<OperationResult> ResetAdminPasswordAsync(IReadOnlyList<string> answers, string newPassword, CancellationToken cancellationToken = default)
    {
        if (newPassword.Length < 10) return OperationResult.Fail("Yeni şifre en az 10 karakter olmalıdır.");
        if (answers.Count != 3) return OperationResult.Fail("Üç güvenlik sorusu cevaplanmalıdır.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var stored = await db.SecurityAnswers.OrderBy(x => x.Order).ToListAsync(cancellationToken);
        if (stored.Count != 3) return OperationResult.Fail("Güvenlik soruları bulunamadı.");
        for (var i = 0; i < 3; i++)
        {
            if (!PasswordHasher.Verify(PasswordHasher.NormalizeAnswer(answers[i]), stored[i].AnswerHash, stored[i].AnswerSalt))
                return OperationResult.Fail("Güvenlik sorularından biri veya daha fazlası hatalı.");
        }

        var credential = await db.AdminCredentials.SingleAsync(cancellationToken);
        var hash = PasswordHasher.Hash(newPassword);
        credential.PasswordHash = hash.Hash;
        credential.PasswordSalt = hash.Salt;
        credential.FailedAttempts = 0;
        credential.LockedUntilUtc = null;
        db.AuditEntries.Add(new AuditEntry
        {
            ActivityType = ActivityType.AdminPasswordReset,
            ActorName = "Kurtarma",
            EntityType = nameof(AdminCredential),
            EntityId = credential.Id,
            Description = "Yönetici şifresi güvenlik sorularıyla sıfırlandı."
        });
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Yönetici şifresi değiştirildi.");
    }

    public string EncryptSensitive(string plainText) => protector.Encrypt(plainText);
    public string DecryptSensitive(string cipherText) => protector.Decrypt(cipherText);
}

