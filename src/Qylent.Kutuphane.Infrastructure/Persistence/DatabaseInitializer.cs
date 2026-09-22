using Microsoft.EntityFrameworkCore;

namespace Qylent.Kutuphane.Infrastructure.Persistence;

public sealed class DatabaseInitializer(IDbContextFactory<LibraryDbContext> contextFactory)
{
    public const int CurrentSchemaVersion = 2;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await db.Database.EnsureCreatedAsync(cancellationToken);
        var schema = await db.SchemaInfo.SingleOrDefaultAsync(x => x.Id == 1, cancellationToken);
        if (schema is null)
        {
            db.SchemaInfo.Add(new() { Id = 1, Version = CurrentSchemaVersion });
            await db.SaveChangesAsync(cancellationToken);
            return;
        }

        if (schema.Version > CurrentSchemaVersion)
        {
            throw new InvalidOperationException("Veritabanı bu uygulama sürümünden daha yeni.");
        }

        if (schema.Version == 1)
        {
            await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE LibraryProfiles ADD COLUMN ThemePreference INTEGER NOT NULL DEFAULT 2;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE MemberFieldDefinitions ADD COLUMN ProfileKey TEXT NULL;", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.core.class' WHERE Name = 'Sınıf';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.core.unit' WHERE Name = 'Birim';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.school.guardian-name' WHERE Name = 'Veli adı';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.school.guardian-phone' WHERE Name = 'Veli telefonu';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.public.birth-date' WHERE Name = 'Doğum tarihi';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.public.membership-type' WHERE Name = 'Üyelik türü';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.private.employee-number' WHERE Name = 'Sicil numarası';", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE Members SET ClassOrUnit = COALESCE(NULLIF(ClassOrUnit, ''), (SELECT MemberFieldValues.Value FROM MemberFieldValues JOIN MemberFieldDefinitions ON MemberFieldDefinitions.Id = MemberFieldValues.DefinitionId WHERE MemberFieldValues.MemberId = Members.Id AND MemberFieldDefinitions.ProfileKey IN ('profile.core.class', 'profile.core.unit') AND MemberFieldValues.Value IS NOT NULL AND MemberFieldValues.Value != '' LIMIT 1));", cancellationToken);
            await db.Database.ExecuteSqlRawAsync("UPDATE MemberFieldDefinitions SET IsEnabled = 0 WHERE ProfileKey IN ('profile.core.class', 'profile.core.unit');", cancellationToken);
            schema.Version = CurrentSchemaVersion;
            schema.AppliedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (schema.Version < CurrentSchemaVersion)
            throw new InvalidOperationException("Veritabanı geçişi eksik. Uygulamayı güncelleyin.");
    }
}
