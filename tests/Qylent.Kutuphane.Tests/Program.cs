using ClosedXML.Excel;
using Microsoft.Extensions.DependencyInjection;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;
using Qylent.Kutuphane.Infrastructure.Services;
using System.Diagnostics;
using System.Text.Json;

namespace Qylent.Kutuphane.Tests;

internal static class Program
{
    private static int _passed;
    private static int _failed;

    public static async Task<int> Main(string[] args)
    {
        if (args.Contains("--performance", StringComparer.OrdinalIgnoreCase))
            return await RunPerformanceValidationAsync();

        await RunAsync("İlk kurulum ve yönetici girişi", SetupAndAdminLoginAsync);
        await RunAsync("Hassas alan şifreleme", SensitiveDataRoundTripAsync);
        await RunAsync("Yinelenen barkod engeli", DuplicateBarcodeIsRejectedAsync);
        await RunAsync("Ödünç sınırı ve ayırtma sırası", CirculationRulesAsync);
        await RunAsync("Excel raporu yapısı ve tarih hücreleri", ReportWorkbookAsync);
        await RunAsync("Şifreli yedek, yanlış parola ve geri yükleme", BackupRoundTripAsync);
        await RunAsync("Güvenlik sorularıyla şifre sıfırlama", PasswordResetAsync);
        await RunAsync("Günlük otomatik yedek", AutomaticBackupAsync);
        await RunAsync("Kitap ve üye düzenleme / ilişkisiz silme", CatalogAndMemberManagementAsync);
        await RunAsync("Geçmişli kayıtları silmeyip arşivleme", HistoricalRecordsAreArchivedAsync);
        await RunAsync("Ayarlar ve rapor şablonu yönetimi", SettingsAndReportPresetsAsync);
        Console.WriteLine($"Sonuç: {_passed} başarılı, {_failed} başarısız.");
        return _failed == 0 ? 0 : 1;
    }

    private static async Task<int> RunPerformanceValidationAsync()
    {
        try
        {
            foreach (var recordCount in new[] { 50_000, 250_000 })
                await ValidateCatalogPerformanceAsync(recordCount);
            Console.WriteLine("Performans sonucu: 2/2 veri ölçeği başarılı.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Performans doğrulaması başarısız: {exception.Message}");
            return 1;
        }
    }

    private static async Task ValidateCatalogPerformanceAsync(int recordCount)
    {
        const int batchSize = 2_500;
        var titleCount = recordCount / 2;
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "qylent-kutuphane-performance"));
        var directory = Path.Combine(root, recordCount.ToString());
        if (Directory.Exists(directory)) Directory.Delete(directory, true);

        var services = new ServiceCollection();
        services.AddQylentKutuphaneInfrastructure(directory);
        services.AddSingleton<IKeyProtectionProvider, TestKeyProtectionProvider>();
        await using var provider = services.BuildServiceProvider();

        var startup = Stopwatch.StartNew();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        startup.Stop();

        var seed = Stopwatch.StartNew();
        var factory = provider.GetRequiredService<Microsoft.EntityFrameworkCore.IDbContextFactory<LibraryDbContext>>();
        await using (var db = await factory.CreateDbContextAsync())
        {
            db.ChangeTracker.AutoDetectChangesEnabled = false;
            for (var i = 0; i < titleCount; i++)
            {
                var titleId = Guid.NewGuid();
                db.BookTitles.Add(new BookTitle
                {
                    Id = titleId,
                    Title = $"Performans Kitabı {i:D7}",
                    Authors = $"Yazar {i % 1_000:D4}",
                    Isbn = $"978{i:D10}",
                    Category = "Performans",
                    Language = "Türkçe"
                });
                db.BookCopies.Add(new BookCopy
                {
                    BookTitleId = titleId,
                    Barcode = $"PF-{i:D7}",
                    ShelfLocation = $"R-{i % 500:D3}"
                });

                if ((i + 1) % batchSize == 0)
                {
                    await db.SaveChangesAsync();
                    db.ChangeTracker.Clear();
                }
            }
            if (db.ChangeTracker.HasChanges()) await db.SaveChangesAsync();
        }
        seed.Stop();

        var catalog = provider.GetRequiredService<ICatalogService>();
        var lastIndex = titleCount - 1;
        var barcodeSearch = Stopwatch.StartNew();
        var barcodeResult = await catalog.SearchAsync($"PF-{lastIndex:D7}");
        barcodeSearch.Stop();
        Require(barcodeResult.Count == 1 && barcodeResult[0].Barcode == $"PF-{lastIndex:D7}",
            $"{recordCount:N0} kayıtta barkod araması doğru sonucu vermedi.");

        var titleSearch = Stopwatch.StartNew();
        var titleResult = await catalog.SearchAsync($"Performans Kitabı {lastIndex:D7}");
        titleSearch.Stop();
        Require(titleResult.Count == 1 && titleResult[0].Title == $"Performans Kitabı {lastIndex:D7}",
            $"{recordCount:N0} kayıtta kitap adı araması doğru sonucu vermedi.");

        var listSearch = Stopwatch.StartNew();
        var listResult = await catalog.SearchAsync("");
        listSearch.Stop();
        Require(listResult.Count == 100, $"{recordCount:N0} kayıtta ilk katalog sayfası alınamadı.");

        var slowestQuery = new[] { barcodeSearch.Elapsed, titleSearch.Elapsed, listSearch.Elapsed }.Max();
        Require(slowestQuery <= TimeSpan.FromSeconds(3),
            $"{recordCount:N0} kayıtta sorgu süresi 3 saniyelik bütçeyi aştı ({slowestQuery.TotalMilliseconds:N0} ms).");

        var databaseBytes = new FileInfo(provider.GetRequiredService<AppPaths>().DatabasePath).Length;
        Console.WriteLine(
            $"[BAŞARILI] {recordCount:N0} kayıt | başlangıç {startup.Elapsed.TotalMilliseconds:N0} ms | " +
            $"veri hazırlama {seed.Elapsed.TotalSeconds:N1} sn | barkod {barcodeSearch.Elapsed.TotalMilliseconds:N0} ms | " +
            $"kitap adı {titleSearch.Elapsed.TotalMilliseconds:N0} ms | liste {listSearch.Elapsed.TotalMilliseconds:N0} ms | " +
            $"veritabanı {databaseBytes / 1024d / 1024d:N1} MB");

        await provider.DisposeAsync();
        var resolved = Path.GetFullPath(directory);
        if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            Directory.Delete(resolved, true);
    }

    private static async Task SetupAndAdminLoginAsync()
    {
        await WithServicesAsync(async provider =>
        {
            var setup = provider.GetRequiredService<ISetupService>();
            var paths = provider.GetRequiredService<AppPaths>();
            var result = await setup.CompleteSetupAsync(new SetupRequest(
                "Deneme Kütüphanesi", null, LibraryType.School, OperatorMode.NameSelection,
                14, 5, 1, paths.DefaultBackupDirectory, "GuvenliSifre42!",
                [new("İlk okulunuz?", "Deneme"), new("En sevdiğiniz kitap?", "Kitap"), new("Doğduğunuz şehir?", "Ankara")]));
            Require(result.Success, result.Message);
            Require(await setup.IsSetupCompletedAsync(), "Kurulum tamamlandı olarak görünmedi.");
            var login = await provider.GetRequiredService<ISecurityService>().LoginAdminAsync("GuvenliSifre42!");
            Require(login.Success && login.Value is not null, login.Message);
        });
    }

    private static async Task SensitiveDataRoundTripAsync()
    {
        await WithServicesAsync(provider =>
        {
            var security = provider.GetRequiredService<ISecurityService>();
            var cipher = security.EncryptSensitive("0555 111 22 33");
            Require(!cipher.Contains("0555", StringComparison.Ordinal), "Şifreli metinde düz veri bulundu.");
            Require(security.DecryptSensitive(cipher) == "0555 111 22 33", "Şifre çözme sonucu eşleşmedi.");
            return Task.CompletedTask;
        });
    }

    private static async Task DuplicateBarcodeIsRejectedAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var catalog = provider.GetRequiredService<ICatalogService>();
            var title = await catalog.AddTitleAsync(new("Sefiller", "Victor Hugo", "9780000000001", null, 1862, "Roman", "Türkçe", null), "Test");
            Require(title.Success && title.Value is not null, title.Message);
            var titleValue = title.Value ?? throw new InvalidOperationException(title.Message);
            var first = await catalog.AddCopyAsync(new(titleValue.Id, "BK-0001", "A-1"), "Test");
            var second = await catalog.AddCopyAsync(new(titleValue.Id, "BK-0001", "A-2"), "Test");
            Require(first.Success, first.Message);
            Require(!second.Success, "Yinelenen barkod kabul edildi.");
        });
    }

    private static async Task CirculationRulesAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 1);
            var members = provider.GetRequiredService<IMemberService>();
            var firstMember = await members.AddAsync(new("U-1", "Ayşe Yılmaz", "Öğrenci", "5-A", null, null, null), "Test");
            var secondMember = await members.AddAsync(new("U-2", "Mehmet Kaya", "Öğrenci", "5-B", null, null, null), "Test");
            Require(firstMember.Success && secondMember.Success, "Üyeler eklenemedi.");

            var catalog = provider.GetRequiredService<ICatalogService>();
            var firstTitle = await catalog.AddTitleAsync(new("Küçük Prens", "Antoine de Saint-Exupéry", null, null, 1943, "Çocuk", "Türkçe", null), "Test");
            var secondTitle = await catalog.AddTitleAsync(new("Çalıkuşu", "Reşat Nuri Güntekin", null, null, 1922, "Roman", "Türkçe", null), "Test");
            Require(firstTitle.Value is not null && secondTitle.Value is not null, "Kitaplar eklenemedi.");
            var firstTitleValue = firstTitle.Value ?? throw new InvalidOperationException("İlk kitap eklenemedi.");
            var secondTitleValue = secondTitle.Value ?? throw new InvalidOperationException("İkinci kitap eklenemedi.");
            Require((await catalog.AddCopyAsync(new(firstTitleValue.Id, "KP-1", "Ç-1"), "Test")).Success, "İlk kopya eklenemedi.");
            Require((await catalog.AddCopyAsync(new(secondTitleValue.Id, "CK-1", "R-1"), "Test")).Success, "İkinci kopya eklenemedi.");

            var circulation = provider.GetRequiredService<ICirculationService>();
            var loan = await circulation.CheckoutAsync(new("U-1", "KP-1"));
            Require(loan.Success && loan.Value is not null, loan.Message);
            var loanValue = loan.Value ?? throw new InvalidOperationException(loan.Message);
            var overLimit = await circulation.CheckoutAsync(new("U-1", "CK-1"));
            Require(!overLimit.Success, "Aktif ödünç sınırı uygulanmadı.");
            var reservation = await circulation.ReserveAsync(new("U-2", firstTitleValue.Id));
            Require(reservation.Success && reservation.Value?.Status == ReservationStatus.Waiting, reservation.Message);
            var renewal = await circulation.RenewAsync(loanValue.LoanId);
            Require(!renewal.Success, "Bekleyen ayırtmaya rağmen uzatma yapıldı.");
            Require((await circulation.ReturnAsync(new("KP-1"))).Success, "İade başarısız.");
            var reservations = await circulation.GetReservationsAsync();
            Require(reservations.Count == 1 && reservations[0].Status == ReservationStatus.Ready, "İade edilen kopya sıradaki üyeye ayrılmadı.");
        });
    }

    private static async Task ReportWorkbookAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var output = Path.Combine(provider.GetRequiredService<AppPaths>().DataDirectory, "haftalik-rapor.xlsx");
            var today = DateOnly.FromDateTime(DateTime.Today);
            var result = await provider.GetRequiredService<IReportService>().ExportAsync(new(
                ReportPeriodKind.Weekly, today, today, ["Tarih", "İşlem", "Görevli"], true, true, output));
            Require(result.Success && File.Exists(output), result.Message);
            using var workbook = new XLWorkbook(output);
            Require(workbook.Worksheets.Select(x => x.Name).SequenceEqual(["Özet", "İşlemler", "Gecikenler", "Ayırtmalar"]), "Rapor sayfaları beklenen yapıda değil.");
            Require(workbook.Worksheet("Özet").Cell("B3").DataType == XLDataType.DateTime, "Başlangıç tarihi gerçek Excel tarihi değil.");
            var expectedMonday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)).ToDateTime(TimeOnly.MinValue);
            Require(workbook.Worksheet("Özet").Cell("B3").GetDateTime() == expectedMonday, "Haftalık dönem Pazartesi başlamıyor.");
        });
    }

    private static async Task BackupRoundTripAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var paths = provider.GetRequiredService<AppPaths>();
            var backupPath = Path.Combine(paths.DataDirectory, "deneme.qylibbackup");
            var backups = provider.GetRequiredService<IBackupService>();
            var created = await backups.CreateAsync(new(backupPath, "UzunYedekParolasi42!", false));
            Require(created.Success && File.Exists(backupPath), created.Message);

            var members = provider.GetRequiredService<IMemberService>();
            Require((await members.AddAsync(new("SONRADAN", "Sonradan Eklenen", "Genel", null, null, null, null), "Test")).Success, "Test üyesi eklenemedi.");
            var wrongPassword = await backups.RestoreAsync(new(backupPath, "YanlisParola42!"));
            Require(!wrongPassword.Success, "Yanlış yedek parolası kabul edildi.");
            var restored = await backups.RestoreAsync(new(backupPath, "UzunYedekParolasi42!"));
            Require(restored.Success, restored.Message);
            Require((await members.SearchAsync("SONRADAN")).Count == 0, "Geri yükleme eski veriyi yerine koymadı.");
        });
    }

    private static async Task PasswordResetAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var security = provider.GetRequiredService<ISecurityService>();
            Require(!(await security.ResetAdminPasswordAsync(["hatalı", "Cevap 2", "Cevap 3"], "YeniGuvenli42!")).Success, "Hatalı güvenlik cevabı kabul edildi.");
            Require((await security.ResetAdminPasswordAsync(["Cevap 1", "Cevap 2", "Cevap 3"], "YeniGuvenli42!")).Success, "Şifre sıfırlanamadı.");
            Require((await security.LoginAdminAsync("YeniGuvenli42!")).Success, "Yeni şifreyle giriş yapılamadı.");
        });
    }

    private static async Task AutomaticBackupAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var coordinator = provider.GetRequiredService<AutomaticBackupCoordinator>();
            await coordinator.RunIfDueAsync();
            var paths = provider.GetRequiredService<AppPaths>();
            var backups = Directory.GetFiles(paths.AutomaticBackupDirectory, "*.qylibbackup");
            Require(backups.Length == 1, "Otomatik yedek oluşturulmadı.");
            await coordinator.RunIfDueAsync();
            Require(Directory.GetFiles(paths.AutomaticBackupDirectory, "*.qylibbackup").Length == 1, "Aynı gün ikinci otomatik yedek oluşturuldu.");
        });
    }

    private static async Task CatalogAndMemberManagementAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var administration = provider.GetRequiredService<IAdministrationService>();
            Require((await administration.AddMemberFieldAsync(new("Mezuniyet yılı", MemberFieldType.Number, true, false, null))).Success, "Özel üye alanı eklenemedi.");
            var graduationField = (await administration.GetMemberFieldsAsync()).Single(x => x.Name == "Mezuniyet yılı");
            var catalog = provider.GetRequiredService<ICatalogService>();
            var title = await catalog.AddTitleAsync(new("Yanlış Ad", "Yazar", null, null, null, null, "Türkçe", null), "Test");
            var titleValue = title.Value ?? throw new InvalidOperationException(title.Message);
            var copy = await catalog.AddCopyAsync(new(titleValue.Id, "DUZ-1", "A-1"), "Test");
            var copyValue = copy.Value ?? throw new InvalidOperationException(copy.Message);
            Require((await catalog.UpdateTitleAsync(titleValue.Id, new("Doğru Ad", "Doğru Yazar", "9781234567890", null, 2020, "Roman", "Türkçe", null), "Test")).Success, "Kitap güncellenemedi.");
            Require((await catalog.UpdateCopyAsync(copyValue.Id, "DUZ-2", "B-2", "Test")).Success, "Kopya güncellenemedi.");
            var found = await catalog.SearchAsync("DUZ-2"); Require(found.Count == 1 && found[0].Title == "Doğru Ad" && found[0].ShelfLocation == "B-2", "Güncellenmiş kitap bulunamadı.");
            Require((await catalog.DeleteCopyAsync(copyValue.Id, "Test")).Success, "İlişkisiz kopya silinemedi.");
            Require((await catalog.DeleteTitleAsync(titleValue.Id, "Test")).Success, "İlişkisiz kitap silinemedi.");

            var members = provider.GetRequiredService<IMemberService>();
            Require(!(await members.AddAsync(new("BOZUK", "Eksik Alan", "Genel", null, null, null, null), "Test")).Success, "Zorunlu özel alan atlandı.");
            Require(!(await members.AddAsync(new("BOZUK", "Yanlış Alan", "Genel", null, null, null, null, new Dictionary<Guid, string?> { [graduationField.Id] = "sayı değil" }), "Test")).Success, "Özel alan tür doğrulaması çalışmadı.");
            var member = await members.AddAsync(new("H-1", "Hatalı Ad", "Genel", null, null, null, null, new Dictionary<Guid, string?> { [graduationField.Id] = "2026" }), "Test");
            var memberValue = member.Value ?? throw new InvalidOperationException(member.Message);
            Require((await members.UpdateAsync(memberValue.Id, new("D-1", "Doğru Ad", "Öğrenci", "7-A", null, null, null, new Dictionary<Guid, string?> { [graduationField.Id] = "2027" }), "Test")).Success, "Üye güncellenemedi.");
            Require((await members.SearchAsync("D-1")).Single().FullName == "Doğru Ad", "Güncellenmiş üye bulunamadı.");
            Require((await members.DeleteAsync(memberValue.Id, "Test")).Success, "İlişkisiz üye silinemedi.");
        });
    }

    private static async Task HistoricalRecordsAreArchivedAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var members = provider.GetRequiredService<IMemberService>();
            var member = (await members.AddAsync(new("G-1", "Geçmişli Üye", "Genel", null, null, null, null), "Test")).Value!;
            var catalog = provider.GetRequiredService<ICatalogService>();
            var title = (await catalog.AddTitleAsync(new("Geçmişli Kitap", "Yazar", null, null, null, null, "Türkçe", null), "Test")).Value!;
            var copy = (await catalog.AddCopyAsync(new(title.Id, "GEC-1", null), "Test")).Value!;
            var circulation = provider.GetRequiredService<ICirculationService>();
            Require((await circulation.CheckoutAsync(new("G-1", "GEC-1"))).Success, "Ödünç verilemedi.");
            Require((await circulation.ReturnAsync(new("GEC-1"))).Success, "İade alınamadı.");
            Require(!(await catalog.DeleteCopyAsync(copy.Id, "Test")).Success, "Geçmişli kopya kalıcı silindi.");
            Require(!(await members.DeleteAsync(member.Id, "Test")).Success, "Geçmişli üye kalıcı silindi.");
            Require((await catalog.ArchiveTitleAsync(title.Id, "Test")).Success, "Geçmişli kitap arşivlenemedi.");
            Require((await members.ArchiveAsync(member.Id, "Test")).Success, "Geçmişli üye arşivlenemedi.");
        });
    }

    private static async Task SettingsAndReportPresetsAsync()
    {
        await WithServicesAsync(async provider =>
        {
            await CompleteSetupAsync(provider, maxLoans: 5);
            var administration = provider.GetRequiredService<IAdministrationService>();
            var backupDirectory = Path.Combine(provider.GetRequiredService<AppPaths>().DataDirectory, "OzelYedekler");
            var settings = await administration.UpdateLibrarySettingsAsync(new(
                "Yeni Kütüphane Adı", "logo.png", LibraryType.Public, OperatorMode.Pin, 21, 8, 2, backupDirectory));
            Require(settings.Success, settings.Message);
            var profile = await administration.GetLibraryProfileAsync();
            Require(profile is not null && profile.Name == "Yeni Kütüphane Adı" && profile.DefaultLoanDays == 21 && profile.BackupDirectory == Path.GetFullPath(backupDirectory), "Ayarlar kalıcı olmadı.");

            var created = await administration.SaveReportPresetAsync(new(null, "Haftalık sade", ["Tarih", "İşlem"], true, false));
            Require(created.Success && created.Value is not null, created.Message);
            var preset = (await administration.GetReportPresetsAsync()).Single();
            Require(JsonSerializer.Deserialize<string[]>(preset.ColumnsJson)!.SequenceEqual(["Tarih", "İşlem"]), "Şablon sütunları saklanmadı.");
            var updated = await administration.SaveReportPresetAsync(new(preset.Id, "Haftalık ayrıntılı", ["Tarih", "İşlem", "Açıklama"], false, true));
            Require(updated.Success && (await administration.GetReportPresetsAsync()).Single().IncludeReservationsSheet, "Rapor şablonu güncellenmedi.");
            Require((await administration.DeleteReportPresetAsync(preset.Id)).Success, "Rapor şablonu silinemedi.");
            Require((await administration.GetReportPresetsAsync()).Count == 0, "Silinen rapor şablonu listede kaldı.");
        });
    }

    private static async Task CompleteSetupAsync(ServiceProvider provider, int maxLoans)
    {
        var paths = provider.GetRequiredService<AppPaths>();
        var result = await provider.GetRequiredService<ISetupService>().CompleteSetupAsync(new SetupRequest(
            "Test Kütüphanesi", null, LibraryType.School, OperatorMode.Shared,
            14, maxLoans, 1, paths.DefaultBackupDirectory, "GuvenliSifre42!",
            [new("Soru 1", "Cevap 1"), new("Soru 2", "Cevap 2"), new("Soru 3", "Cevap 3")]));
        Require(result.Success, result.Message);
    }

    private static async Task WithServicesAsync(Func<ServiceProvider, Task> test)
    {
        var root = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "qylent-kutuphane-tests"));
        var directory = Path.Combine(root, Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();
        services.AddQylentKutuphaneInfrastructure(directory);
        services.AddSingleton<IKeyProtectionProvider, TestKeyProtectionProvider>();
        await using var provider = services.BuildServiceProvider();
        await provider.GetRequiredService<DatabaseInitializer>().InitializeAsync();
        await test(provider);
        await provider.DisposeAsync();
        var resolved = Path.GetFullPath(directory);
        if (resolved.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(resolved))
            Directory.Delete(resolved, true);
    }

    private static async Task RunAsync(string name, Func<Task> test)
    {
        try
        {
            await test();
            _passed++;
            Console.WriteLine($"[BAŞARILI] {name}");
        }
        catch (Exception exception)
        {
            _failed++;
            Console.Error.WriteLine($"[BAŞARISIZ] {name}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class TestKeyProtectionProvider : IKeyProtectionProvider
    {
        public byte[] Protect(byte[] data) => data.ToArray();
        public byte[] Unprotect(byte[] protectedData) => protectedData.ToArray();
    }
}
