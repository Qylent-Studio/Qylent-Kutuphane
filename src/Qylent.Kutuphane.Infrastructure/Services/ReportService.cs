using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class ReportService(IDbContextFactory<LibraryDbContext> contextFactory) : IReportService
{
    private static readonly IReadOnlyDictionary<string, Func<AuditEntry, object?>> Columns =
        new Dictionary<string, Func<AuditEntry, object?>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Tarih"] = x => x.OccurredAtUtc.ToLocalTime().DateTime,
            ["İşlem"] = x => ActivityName(x.ActivityType),
            ["Görevli"] = x => x.ActorName,
            ["Kayıt Türü"] = x => EntityName(x.EntityType),
            ["Açıklama"] = x => x.Description ?? string.Empty
        };

    public async Task<OperationResult<string>> ExportAsync(ReportRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            var (start, end) = ResolvePeriod(request);
            var startUtc = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeZoneInfo.Local.GetUtcOffset(start.ToDateTime(TimeOnly.MinValue))).ToUniversalTime();
            var endExclusiveLocal = end.AddDays(1).ToDateTime(TimeOnly.MinValue);
            var endUtc = new DateTimeOffset(endExclusiveLocal, TimeZoneInfo.Local.GetUtcOffset(endExclusiveLocal)).ToUniversalTime();
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var entries = await db.AuditEntries.AsNoTracking()
                .Where(x => x.OccurredAtUtc >= startUtc && x.OccurredAtUtc < endUtc)
                .OrderBy(x => x.OccurredAtUtc).ToListAsync(cancellationToken);

            var reportNow = DateTimeOffset.UtcNow;
            var overdueCount = await db.Loans.AsNoTracking().CountAsync(x => x.Status == LoanStatus.Active && x.DueAtUtc < reportNow, cancellationToken);
            var selected = request.Columns.Where(Columns.ContainsKey).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (selected.Count == 0) selected = ["Tarih", "İşlem", "Görevli", "Kayıt Türü", "Açıklama"];
            var output = Path.GetFullPath(request.OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(output) ?? Environment.CurrentDirectory);

            using var workbook = new XLWorkbook();
            AddSummary(workbook, entries, start, end, overdueCount);
            AddTransactions(workbook, entries, selected);
            if (request.IncludeOverdueSheet) await AddOverdueAsync(workbook, db, cancellationToken);
            if (request.IncludeReservationsSheet) await AddReservationsAsync(workbook, db, cancellationToken);
            workbook.SaveAs(output);

            CatalogService.AddAudit(db, ActivityType.ReportExported, "Yönetici", nameof(AuditEntry), null,
                new { start, end, output, selected });
            await db.SaveChangesAsync(cancellationToken);
            return OperationResult<string>.Ok(output, "Excel raporu oluşturuldu.");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return OperationResult<string>.Fail($"Rapor oluşturulamadı: {exception.Message}");
        }
    }

    private static (DateOnly Start, DateOnly End) ResolvePeriod(ReportRequest request)
    {
        var anchor = request.StartDate;
        return request.PeriodKind switch
        {
            ReportPeriodKind.Daily => (anchor, anchor),
            ReportPeriodKind.Weekly => (anchor.AddDays(-(((int)anchor.DayOfWeek + 6) % 7)), anchor.AddDays(6 - (((int)anchor.DayOfWeek + 6) % 7))),
            ReportPeriodKind.Monthly => (new DateOnly(anchor.Year, anchor.Month, 1), new DateOnly(anchor.Year, anchor.Month, DateTime.DaysInMonth(anchor.Year, anchor.Month))),
            ReportPeriodKind.Custom when request.EndDate >= request.StartDate => (request.StartDate, request.EndDate),
            ReportPeriodKind.Custom => throw new ArgumentException("Bitiş tarihi başlangıç tarihinden önce olamaz."),
            _ => (request.StartDate, request.EndDate)
        };
    }

    private static void AddSummary(XLWorkbook workbook, IReadOnlyCollection<AuditEntry> entries, DateOnly start, DateOnly end, int overdueCount)
    {
        var sheet = workbook.Worksheets.Add("Özet");
        sheet.Cell("A1").Value = "Qylent Kütüphane — Dönem Raporu";
        sheet.Range("A1:B1").Merge().Style.Font.SetBold().Font.SetFontSize(16).Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2457D6"));
        sheet.Cell("A3").Value = "Başlangıç"; sheet.Cell("B3").Value = start.ToDateTime(TimeOnly.MinValue);
        sheet.Cell("A4").Value = "Bitiş"; sheet.Cell("B4").Value = end.ToDateTime(TimeOnly.MinValue);
        sheet.Range("B3:B4").Style.DateFormat.Format = "dd.MM.yyyy";
        var measures = new (string Label, ActivityType Type)[]
        {
            ("Verilen", ActivityType.Loaned), ("İade edilen", ActivityType.Returned),
            ("Uzatılan", ActivityType.Renewed), ("Geciken (güncel)", (ActivityType)(-1)), ("Ayırtılan", ActivityType.Reserved),
            ("Eklenen kitap/kopya", ActivityType.BookCreated), ("Arşivlenen kitap", ActivityType.BookArchived),
            ("Yeni üye", ActivityType.MemberCreated)
        };
        sheet.Cell("A6").Value = "Ölçü"; sheet.Cell("B6").Value = "Adet";
        for (var i = 0; i < measures.Length; i++)
        {
            sheet.Cell(i + 7, 1).Value = measures[i].Label;
            var count = (int)measures[i].Type == -1 ? overdueCount : measures[i].Type == ActivityType.BookCreated
                ? entries.Count(x => x.ActivityType is ActivityType.BookCreated or ActivityType.CopyCreated)
                : entries.Count(x => x.ActivityType == measures[i].Type);
            sheet.Cell(i + 7, 2).Value = count;
        }
        StyleTable(sheet.Range(6, 1, 6 + measures.Length, 2));
        sheet.Columns().AdjustToContents();
        sheet.Column(1).Width = Math.Max(sheet.Column(1).Width, 24);
    }

    private static void AddTransactions(XLWorkbook workbook, IReadOnlyList<AuditEntry> entries, IReadOnlyList<string> columns)
    {
        var sheet = workbook.Worksheets.Add("İşlemler");
        for (var column = 0; column < columns.Count; column++) sheet.Cell(1, column + 1).Value = columns[column];
        for (var row = 0; row < entries.Count; row++)
        {
            for (var column = 0; column < columns.Count; column++)
            {
                var value = Columns[columns[column]](entries[row]);
                if (value is DateTime date) sheet.Cell(row + 2, column + 1).Value = date;
                else sheet.Cell(row + 2, column + 1).Value = value?.ToString() ?? string.Empty;
            }
        }
        var lastRow = Math.Max(entries.Count + 1, 2);
        StyleTable(sheet.Range(1, 1, lastRow, columns.Count));
        if (entries.Count > 0) sheet.Range(1, 1, entries.Count + 1, columns.Count).CreateTable().ShowAutoFilter = true;
        sheet.SheetView.FreezeRows(1);
        sheet.Columns().AdjustToContents(5, 50);
        var dateColumn = columns.Select((name, index) => (name, index))
            .FirstOrDefault(x => x.name.Equals("Tarih", StringComparison.OrdinalIgnoreCase));
        if (dateColumn.name is not null)
            foreach (var cell in sheet.Column(dateColumn.index + 1).CellsUsed().Skip(1))
                cell.Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
    }

    private static async Task AddOverdueAsync(XLWorkbook workbook, LibraryDbContext db, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var items = await db.Loans.AsNoTracking().Include(x => x.Member).Include(x => x.BookCopy).ThenInclude(x => x.BookTitle)
            .Where(x => x.Status == LoanStatus.Active && x.DueAtUtc < now).OrderBy(x => x.DueAtUtc).ToListAsync(cancellationToken);
        var sheet = workbook.Worksheets.Add("Gecikenler");
        string[] headers = ["Üye No", "Üye", "Kitap", "Barkod", "Teslim Tarihi", "Gecikme Günü"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        for (var i = 0; i < items.Count; i++)
        {
            var loan = items[i];
            sheet.Cell(i + 2, 1).Value = loan.Member.MemberNumber; sheet.Cell(i + 2, 2).Value = loan.Member.FullName;
            sheet.Cell(i + 2, 3).Value = loan.BookCopy.BookTitle.Title; sheet.Cell(i + 2, 4).Value = loan.BookCopy.Barcode;
            sheet.Cell(i + 2, 5).Value = loan.DueAtUtc.ToLocalTime().DateTime;
            sheet.Cell(i + 2, 6).Value = Math.Max(0, (DateTime.Today - loan.DueAtUtc.LocalDateTime.Date).Days);
        }
        FinishListSheet(sheet, items.Count, headers.Length);
        sheet.Column(5).Style.DateFormat.Format = "dd.MM.yyyy";
    }

    private static async Task AddReservationsAsync(XLWorkbook workbook, LibraryDbContext db, CancellationToken cancellationToken)
    {
        var items = await db.Reservations.AsNoTracking().Include(x => x.Member).Include(x => x.BookTitle)
            .Where(x => x.Status == ReservationStatus.Waiting || x.Status == ReservationStatus.Ready)
            .OrderBy(x => x.BookTitle.Title).ThenBy(x => x.RequestedAtUtc).ToListAsync(cancellationToken);
        var sheet = workbook.Worksheets.Add("Ayırtmalar");
        string[] headers = ["Üye No", "Üye", "Kitap", "İstek Tarihi", "Durum"];
        for (var i = 0; i < headers.Length; i++) sheet.Cell(1, i + 1).Value = headers[i];
        for (var i = 0; i < items.Count; i++)
        {
            var item = items[i];
            sheet.Cell(i + 2, 1).Value = item.Member.MemberNumber; sheet.Cell(i + 2, 2).Value = item.Member.FullName;
            sheet.Cell(i + 2, 3).Value = item.BookTitle.Title; sheet.Cell(i + 2, 4).Value = item.RequestedAtUtc.ToLocalTime().DateTime;
            sheet.Cell(i + 2, 5).Value = item.Status == ReservationStatus.Ready ? "Hazır" : "Bekliyor";
        }
        FinishListSheet(sheet, items.Count, headers.Length);
        sheet.Column(4).Style.DateFormat.Format = "dd.MM.yyyy HH:mm";
    }

    private static void FinishListSheet(IXLWorksheet sheet, int count, int columnCount)
    {
        StyleTable(sheet.Range(1, 1, Math.Max(count + 1, 2), columnCount));
        if (count > 0) sheet.Range(1, 1, count + 1, columnCount).CreateTable();
        sheet.SheetView.FreezeRows(1); sheet.Columns().AdjustToContents(5, 45);
    }

    private static void StyleTable(IXLRange range)
    {
        range.FirstRow().Style.Font.SetBold().Font.SetFontColor(XLColor.White).Fill.SetBackgroundColor(XLColor.FromHtml("#2457D6"));
        range.Style.Border.InsideBorder = XLBorderStyleValues.Hair;
        range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
    }

    private static string ActivityName(ActivityType type) => type switch
    {
        ActivityType.Loaned => "Ödünç verildi", ActivityType.Returned => "İade alındı", ActivityType.Renewed => "Uzatıldı",
        ActivityType.Reserved => "Ayırtıldı", ActivityType.MemberCreated => "Üye eklendi", ActivityType.MemberArchived => "Üye arşivlendi",
        ActivityType.BookCreated => "Kitap eklendi", ActivityType.CopyCreated => "Kopya eklendi", ActivityType.BookArchived => "Kitap arşivlendi",
        _ => type.ToString()
    };

    private static string EntityName(string type) => type switch
    {
        nameof(Member) => "Üye", nameof(BookTitle) => "Kitap", nameof(BookCopy) => "Kitap kopyası",
        nameof(Loan) => "Ödünç", nameof(Reservation) => "Ayırtma", _ => type
    };
}
