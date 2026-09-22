using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class BackupService(AppPaths paths, IDbContextFactory<LibraryDbContext> contextFactory, Security.IKeyProtectionProvider keyProtection) : IBackupService
{
    private static readonly byte[] Magic = "QYLIBBK1"u8.ToArray();
    private const int Iterations = 600_000;

    public async Task<OperationResult<string>> CreateAsync(BackupRequest request, CancellationToken cancellationToken = default)
    {
        var password = request.IsAutomatic ? GetAutomaticPassword() : request.Password;
        if (password.Length < 10) return OperationResult<string>.Fail("Yedek parolası en az 10 karakter olmalıdır.");
        try
        {
            var output = Path.GetFullPath(request.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? paths.DefaultBackupDirectory);
            var tempDb = Path.Combine(Path.GetTempPath(), $"qylib-{Guid.NewGuid():N}.db");
            try
            {
                await CreateDatabaseSnapshotAsync(tempDb, cancellationToken);
                var package = BuildPackage(tempDb);
                var encrypted = Encrypt(package, password);
                await File.WriteAllBytesAsync(output, encrypted, cancellationToken);
                var sha = Convert.ToHexString(SHA256.HashData(encrypted));
                await RecordAsync(output, request.IsAutomatic, encrypted.LongLength, sha, true, cancellationToken);
                if (request.IsAutomatic) await PruneAutomaticBackupsAsync(30, cancellationToken);
                return OperationResult<string>.Ok(output, "Şifreli yedek oluşturuldu.");
            }
            finally
            {
                if (File.Exists(tempDb)) File.Delete(tempDb);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or CryptographicException or SqliteException)
        {
            return OperationResult<string>.Fail($"Yedek oluşturulamadı: {exception.Message}");
        }
    }

    public async Task<OperationResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(request.BackupPath)) return OperationResult.Fail("Yedek dosyası bulunamadı.");
        var tempRoot = Path.Combine(Path.GetTempPath(), $"qylib-restore-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            var fullBackupPath = Path.GetFullPath(request.BackupPath);
            await using var lookupDb = await contextFactory.CreateDbContextAsync(cancellationToken);
            var isRecordedAutomatic = await lookupDb.BackupRecords.AsNoTracking()
                .AnyAsync(x => x.IsAutomatic && x.FilePath == fullBackupPath, cancellationToken);
            var password = string.IsNullOrEmpty(request.Password) && (isRecordedAutomatic || fullBackupPath.StartsWith(Path.GetFullPath(paths.AutomaticBackupDirectory), StringComparison.OrdinalIgnoreCase))
                ? GetAutomaticPassword() : request.Password;
            var decrypted = Decrypt(await File.ReadAllBytesAsync(request.BackupPath, cancellationToken), password);
            using (var archive = new ZipArchive(new MemoryStream(decrypted), ZipArchiveMode.Read))
            {
                var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("Yedek bildirimi eksik.");
                BackupManifest manifest;
                using (var manifestStream = manifestEntry.Open())
                    manifest = await JsonSerializer.DeserializeAsync<BackupManifest>(manifestStream, cancellationToken: cancellationToken)
                        ?? throw new InvalidDataException("Yedek bildirimi okunamadı.");
                if (manifest.SchemaVersion is < 1 or > DatabaseInitializer.CurrentSchemaVersion) return OperationResult.Fail("Yedek sürümü bu uygulamayla uyumlu değil.");
                var databaseEntry = archive.GetEntry("kutuphane.db") ?? throw new InvalidDataException("Yedek veritabanı eksik.");
                var restoreDb = Path.Combine(tempRoot, "kutuphane.db");
                await using (var input = databaseEntry.Open())
                await using (var output = File.Create(restoreDb)) await input.CopyToAsync(output, cancellationToken);
                if (!Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(restoreDb, cancellationToken))).Equals(manifest.DatabaseSha256, StringComparison.OrdinalIgnoreCase))
                    return OperationResult.Fail("Yedek bütünlük doğrulamasını geçemedi.");
                await UpgradeRestoredDatabaseAsync(restoreDb, cancellationToken);
                await ValidateDatabaseAsync(restoreDb, cancellationToken);
                var keyEntry = archive.GetEntry("sensitive-fields.key");
                if (keyEntry is not null)
                {
                    await using var keyInput = keyEntry.Open();
                    await using var keyOutput = File.Create(Path.Combine(tempRoot, "sensitive-fields.key"));
                    await keyInput.CopyToAsync(keyOutput, cancellationToken);
                }
            }

            Directory.CreateDirectory(paths.DefaultBackupDirectory);
            var safetyPath = Path.Combine(paths.DefaultBackupDirectory, $"geri-yukleme-oncesi-{DateTime.Now:yyyyMMdd-HHmmss}.qylibbackup");
            var safety = await CreateAsync(new BackupRequest(safetyPath, password, false), cancellationToken);
            if (!safety.Success) return OperationResult.Fail($"Güvenlik yedeği alınamadı. Geri yükleme durduruldu: {safety.Message}");

            File.Copy(Path.Combine(tempRoot, "kutuphane.db"), paths.DatabasePath, true);
            var restoredKey = Path.Combine(tempRoot, "sensitive-fields.key");
            if (File.Exists(restoredKey)) File.Copy(restoredKey, paths.KeyPath, true);
            await RecordAsync(request.BackupPath, false, new FileInfo(request.BackupPath).Length,
                Convert.ToHexString(SHA256.HashData(await File.ReadAllBytesAsync(request.BackupPath, cancellationToken))), true, cancellationToken);
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            CatalogService.AddAudit(db, ActivityType.BackupRestored, "Yönetici", nameof(BackupRecord), null, new { request.BackupPath, safetyPath });
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult.Ok("Yedek doğrulandı ve geri yüklendi.");
        }
        catch (CryptographicException)
        {
            return OperationResult.Fail("Yedek parolası yanlış veya dosya bozuk.");
        }
        catch (Exception exception) when (exception is IOException or InvalidDataException or UnauthorizedAccessException or SqliteException)
        {
            return OperationResult.Fail($"Yedek geri yüklenemedi: {exception.Message}");
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, true);
        }
    }

    public async Task PruneAutomaticBackupsAsync(int keepCount = 30, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var old = await db.BackupRecords.Where(x => x.IsAutomatic && x.IsSuccessful)
            .OrderByDescending(x => x.CreatedAtUtc).Skip(Math.Max(keepCount, 0)).ToListAsync(cancellationToken);
        foreach (var record in old)
        {
            if (File.Exists(record.FilePath)) File.Delete(record.FilePath);
            db.BackupRecords.Remove(record);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateDatabaseSnapshotAsync(string target, CancellationToken cancellationToken)
    {
        await using var source = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False");
        await using var destination = new SqliteConnection($"Data Source={target};Pooling=False");
        await source.OpenAsync(cancellationToken); await destination.OpenAsync(cancellationToken);
        source.BackupDatabase(destination);
    }

    private byte[] BuildPackage(string snapshotPath)
    {
        var database = File.ReadAllBytes(snapshotPath);
        using var memory = new MemoryStream();
        using (var archive = new ZipArchive(memory, ZipArchiveMode.Create, true))
        {
            var dbEntry = archive.CreateEntry("kutuphane.db", CompressionLevel.Optimal);
            using (var output = dbEntry.Open()) output.Write(database);
            if (File.Exists(paths.KeyPath))
            {
                var keyEntry = archive.CreateEntry("sensitive-fields.key", CompressionLevel.Optimal);
                using var output = keyEntry.Open(); output.Write(File.ReadAllBytes(paths.KeyPath));
            }
            var manifest = new BackupManifest(DatabaseInitializer.CurrentSchemaVersion, DateTimeOffset.UtcNow, Convert.ToHexString(SHA256.HashData(database)));
            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using var manifestOutput = manifestEntry.Open();
            JsonSerializer.Serialize(manifestOutput, manifest);
        }
        return memory.ToArray();
    }

    private static byte[] Encrypt(byte[] plain, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16); var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[plain.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        using (var aes = new AesGcm(key, 16)) aes.Encrypt(nonce, plain, cipher, tag, Magic);
        using var stream = new MemoryStream(); stream.Write(Magic); stream.Write(salt); stream.Write(nonce); stream.Write(tag); stream.Write(cipher);
        CryptographicOperations.ZeroMemory(key); return stream.ToArray();
    }

    private static byte[] Decrypt(byte[] payload, string password)
    {
        if (payload.Length < 52 || !payload.AsSpan(0, 8).SequenceEqual(Magic)) throw new InvalidDataException("Yedek biçimi tanınmıyor.");
        var salt = payload.AsSpan(8, 16); var nonce = payload.AsSpan(24, 12); var tag = payload.AsSpan(36, 16); var cipher = payload.AsSpan(52); var plain = new byte[cipher.Length];
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        using (var aes = new AesGcm(key, 16)) aes.Decrypt(nonce, cipher, tag, plain, Magic);
        CryptographicOperations.ZeroMemory(key); return plain;
    }

    private static async Task ValidateDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Mode=ReadOnly;Pooling=False"); await connection.OpenAsync(cancellationToken);
        await using var integrity = connection.CreateCommand(); integrity.CommandText = "PRAGMA integrity_check;";
        if (!string.Equals((string?)await integrity.ExecuteScalarAsync(cancellationToken), "ok", StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Veritabanı bütünlük denetimi başarısız.");
        await using var schema = connection.CreateCommand(); schema.CommandText = "SELECT Version FROM SchemaInfo WHERE Id=1;";
        if (Convert.ToInt32(await schema.ExecuteScalarAsync(cancellationToken)) != DatabaseInitializer.CurrentSchemaVersion) throw new InvalidDataException("Veritabanı sürümü uyumsuz.");
    }

    private static async Task UpgradeRestoredDatabaseAsync(string path, CancellationToken cancellationToken)
    {
        await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
        await connection.OpenAsync(cancellationToken);
        await using var readVersion = connection.CreateCommand();
        readVersion.CommandText = "SELECT Version FROM SchemaInfo WHERE Id=1;";
        var version = Convert.ToInt32(await readVersion.ExecuteScalarAsync(cancellationToken));
        if (version == DatabaseInitializer.CurrentSchemaVersion) return;
        if (version != 1) throw new InvalidDataException("Veritabanı sürümü uyumsuz.");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var sql in new[]
        {
            "ALTER TABLE LibraryProfiles ADD COLUMN ThemePreference INTEGER NOT NULL DEFAULT 2;",
            "ALTER TABLE MemberFieldDefinitions ADD COLUMN ProfileKey TEXT NULL;",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.core.class' WHERE Name = 'Sınıf';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.core.unit' WHERE Name = 'Birim';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.school.guardian-name' WHERE Name = 'Veli adı';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.school.guardian-phone' WHERE Name = 'Veli telefonu';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.public.birth-date' WHERE Name = 'Doğum tarihi';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.public.membership-type' WHERE Name = 'Üyelik türü';",
            "UPDATE MemberFieldDefinitions SET ProfileKey = 'profile.private.employee-number' WHERE Name = 'Sicil numarası';",
            "UPDATE Members SET ClassOrUnit = COALESCE(NULLIF(ClassOrUnit, ''), (SELECT MemberFieldValues.Value FROM MemberFieldValues JOIN MemberFieldDefinitions ON MemberFieldDefinitions.Id = MemberFieldValues.DefinitionId WHERE MemberFieldValues.MemberId = Members.Id AND MemberFieldDefinitions.ProfileKey IN ('profile.core.class', 'profile.core.unit') AND MemberFieldValues.Value IS NOT NULL AND MemberFieldValues.Value != '' LIMIT 1));",
            "UPDATE MemberFieldDefinitions SET IsEnabled = 0 WHERE ProfileKey IN ('profile.core.class', 'profile.core.unit');",
            $"UPDATE SchemaInfo SET Version = {DatabaseInitializer.CurrentSchemaVersion}, AppliedAtUtc = {DateTimeOffset.UtcNow.UtcTicks} WHERE Id = 1;"
        })
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
    }

    private async Task RecordAsync(string path, bool automatic, long size, string sha, bool success, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var record = new BackupRecord { FilePath = path, IsAutomatic = automatic, SizeBytes = size, Sha256 = sha, IsSuccessful = success };
        db.BackupRecords.Add(record);
        CatalogService.AddAudit(db, ActivityType.BackupCreated, automatic ? "Sistem" : "Yönetici", nameof(BackupRecord), record.Id, new { path, automatic, size });
        await db.SaveChangesAsync(cancellationToken);
    }

    private sealed record BackupManifest(int SchemaVersion, DateTimeOffset CreatedAtUtc, string DatabaseSha256);

    private string GetAutomaticPassword()
    {
        if (!File.Exists(paths.AutomaticBackupKeyPath))
        {
            var secret = RandomNumberGenerator.GetBytes(32);
            File.WriteAllBytes(paths.AutomaticBackupKeyPath, keyProtection.Protect(secret));
            CryptographicOperations.ZeroMemory(secret);
        }
        var key = keyProtection.Unprotect(File.ReadAllBytes(paths.AutomaticBackupKeyPath));
        try { return Convert.ToBase64String(key); }
        finally { CryptographicOperations.ZeroMemory(key); }
    }
}
