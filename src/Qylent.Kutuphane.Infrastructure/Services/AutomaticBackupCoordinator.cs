using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class AutomaticBackupCoordinator(AppPaths paths, IDbContextFactory<LibraryDbContext> contextFactory, IBackupService backups)
{
    public async Task RunIfDueAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var latest = await db.BackupRecords.AsNoTracking().Where(x => x.IsAutomatic && x.IsSuccessful)
            .OrderByDescending(x => x.CreatedAtUtc).Select(x => (DateTimeOffset?)x.CreatedAtUtc).FirstOrDefaultAsync(cancellationToken);
        if (latest?.ToLocalTime().Date == DateTime.Today) return;
        var configuredDirectory = await db.LibraryProfiles.AsNoTracking().Where(x => x.SetupCompleted)
            .Select(x => x.BackupDirectory).SingleOrDefaultAsync(cancellationToken);
        var automaticDirectory = Path.Combine(string.IsNullOrWhiteSpace(configuredDirectory) ? paths.DefaultBackupDirectory : configuredDirectory, "Otomatik");
        Directory.CreateDirectory(automaticDirectory);
        var path = Path.Combine(automaticDirectory, $"otomatik-{DateTime.Now:yyyyMMdd-HHmmss}.qylibbackup");
        await backups.CreateAsync(new BackupRequest(path, string.Empty, true), cancellationToken);
    }
}
