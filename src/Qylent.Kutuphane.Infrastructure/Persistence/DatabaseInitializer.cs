using Microsoft.EntityFrameworkCore;

namespace Qylent.Kutuphane.Infrastructure.Persistence;

public sealed class DatabaseInitializer(IDbContextFactory<LibraryDbContext> contextFactory)
{
    public const int CurrentSchemaVersion = 1;

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

        if (schema.Version < CurrentSchemaVersion)
        {
            throw new InvalidOperationException("Veritabanı geçişi eksik. Uygulamayı güncelleyin.");
        }
    }
}

