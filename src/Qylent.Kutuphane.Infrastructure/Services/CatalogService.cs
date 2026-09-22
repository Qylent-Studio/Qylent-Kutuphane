using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class CatalogService(IDbContextFactory<LibraryDbContext> contextFactory) : ICatalogService
{
    public async Task<OperationResult<BookCreationResult>> AddTitleWithFirstCopyAsync(BookTitleInput input, string barcode, string? shelfLocation, string actor, CancellationToken cancellationToken = default)
    {
        var validation = ValidateTitle(input);
        if (validation is not null) return OperationResult<BookCreationResult>.Fail(validation);
        barcode = barcode.Trim();
        if (barcode.Length < 2) return OperationResult<BookCreationResult>.Fail("Barkod veya demirbaş kodu zorunludur.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        if (await db.BookCopies.AnyAsync(x => x.Barcode == barcode, cancellationToken))
            return OperationResult<BookCreationResult>.Fail("Bu barkod veya demirbaş kodu zaten kullanılıyor.");

        var title = CreateTitle(input);
        var copy = new BookCopy { BookTitleId = title.Id, Barcode = barcode, ShelfLocation = Clean(shelfLocation) };
        db.BookTitles.Add(title);
        db.BookCopies.Add(copy);
        AddAudit(db, ActivityType.BookCreated, actor, nameof(BookTitle), title.Id,
            new { title.Title, title.Authors, title.Isbn, title.Category });
        AddAudit(db, ActivityType.CopyCreated, actor, nameof(BookCopy), copy.Id,
            new { copy.BookTitleId, copy.Barcode, copy.ShelfLocation });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return OperationResult<BookCreationResult>.Ok(new(title.Id, copy.Id), "Kitap ve ilk kopyası eklendi.");
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return OperationResult<BookCreationResult>.Fail("Kitap kaydedilemedi. Barkodun benzersiz olduğunu kontrol edin.");
        }
    }

    public async Task<OperationResult<BookTitle>> AddTitleAsync(BookTitleInput input, string actor, CancellationToken cancellationToken = default)
    {
        var validation = ValidateTitle(input);
        if (validation is not null) return OperationResult<BookTitle>.Fail(validation);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var title = CreateTitle(input);
        db.BookTitles.Add(title);
        AddAudit(db, ActivityType.BookCreated, actor, nameof(BookTitle), title.Id, title);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<BookTitle>.Ok(title, "Kitap kaydı eklendi.");
    }

    public async Task<OperationResult<BookCopy>> AddCopyAsync(BookCopyInput input, string actor, CancellationToken cancellationToken = default)
    {
        var barcode = input.Barcode.Trim();
        if (barcode.Length < 2) return OperationResult<BookCopy>.Fail("Barkod veya demirbaş kodu zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        if (!await db.BookTitles.AnyAsync(x => x.Id == input.BookTitleId && !x.IsArchived, cancellationToken))
            return OperationResult<BookCopy>.Fail("Aktif kitap kaydı bulunamadı.");
        if (await db.BookCopies.AnyAsync(x => x.Barcode == barcode, cancellationToken))
            return OperationResult<BookCopy>.Fail("Bu barkod veya demirbaş kodu zaten kullanılıyor.");
        var copy = new BookCopy { BookTitleId = input.BookTitleId, Barcode = barcode, ShelfLocation = Clean(input.ShelfLocation) };
        db.BookCopies.Add(copy);
        AddAudit(db, ActivityType.CopyCreated, actor, nameof(BookCopy), copy.Id, copy);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<BookCopy>.Ok(copy, "Kitap kopyası eklendi.");
    }

    public async Task<OperationResult> UpdateTitleAsync(Guid titleId, BookTitleInput input, string actor, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(input.Title) || string.IsNullOrWhiteSpace(input.Authors)) return OperationResult.Fail("Kitap adı ve yazar zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var title = await db.BookTitles.SingleOrDefaultAsync(x => x.Id == titleId, cancellationToken);
        if (title is null) return OperationResult.Fail("Kitap kaydı bulunamadı.");
        var old = new { title.Title, title.Authors, title.Isbn, title.Publisher, title.PublicationYear, title.Category, title.Language, title.Description };
        title.Title = input.Title.Trim(); title.Authors = input.Authors.Trim();
        if (input.Isbn is not null) title.Isbn = Clean(input.Isbn);
        if (input.Publisher is not null) title.Publisher = Clean(input.Publisher);
        if (input.PublicationYear is not null) title.PublicationYear = input.PublicationYear;
        if (input.Category is not null) title.Category = Clean(input.Category);
        if (!string.IsNullOrWhiteSpace(input.Language)) title.Language = input.Language.Trim();
        if (input.Description is not null) title.Description = Clean(input.Description);
        db.AuditEntries.Add(new AuditEntry { ActivityType = ActivityType.BookUpdated, ActorName = actor, EntityType = nameof(BookTitle), EntityId = title.Id, OldStateJson = JsonSerializer.Serialize(old), NewStateJson = JsonSerializer.Serialize(new { title.Title, title.Authors, title.Isbn, title.Publisher, title.PublicationYear, title.Category, title.Language, title.Description }) });
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Kitap bilgileri güncellendi.");
    }

    public async Task<OperationResult> UpdateCopyAsync(Guid copyId, string barcode, string? shelfLocation, string actor, CancellationToken cancellationToken = default)
    {
        barcode = barcode.Trim(); if (barcode.Length < 2) return OperationResult.Fail("Barkod veya demirbaş kodu zorunludur.");
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var copy = await db.BookCopies.SingleOrDefaultAsync(x => x.Id == copyId, cancellationToken);
        if (copy is null) return OperationResult.Fail("Kitap kopyası bulunamadı.");
        if (await db.BookCopies.AnyAsync(x => x.Id != copyId && x.Barcode == barcode, cancellationToken)) return OperationResult.Fail("Bu barkod veya demirbaş kodu zaten kullanılıyor.");
        var old = new { copy.Barcode, copy.ShelfLocation }; copy.Barcode = barcode; copy.ShelfLocation = Clean(shelfLocation);
        db.AuditEntries.Add(new AuditEntry { ActivityType = ActivityType.CopyUpdated, ActorName = actor, EntityType = nameof(BookCopy), EntityId = copy.Id, OldStateJson = JsonSerializer.Serialize(old), NewStateJson = JsonSerializer.Serialize(new { copy.Barcode, copy.ShelfLocation }) });
        await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("Kopya bilgileri güncellendi.");
    }

    public async Task<IReadOnlyList<BookSearchResult>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        query = query.Trim();
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var titles = await db.BookTitles.AsNoTracking().Include(x => x.Copies)
            .Where(x => (includeArchived || !x.IsArchived) &&
                (query == "" || x.Title.Contains(query) || x.Authors.Contains(query) ||
                 (x.Isbn != null && x.Isbn.Contains(query)) || x.Copies.Any(c => c.Barcode.Contains(query))))
            .OrderBy(x => x.Title).Take(100).ToListAsync(cancellationToken);
        return titles.SelectMany(title => title.Copies.Count == 0
                ? [new BookSearchResult(title.Id, null, title.Title, title.Authors, title.Isbn, null, null, null, title.Category, title.IsArchived)]
                : title.Copies.Where(x => includeArchived || x.Status != BookCopyStatus.Archived)
                    .Select(copy => new BookSearchResult(title.Id, copy.Id, title.Title, title.Authors, title.Isbn, copy.Barcode, copy.ShelfLocation, copy.Status, title.Category, title.IsArchived)))
            .Take(250).ToList();
    }

    public async Task<OperationResult> ArchiveTitleAsync(Guid titleId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var title = await db.BookTitles.Include(x => x.Copies).SingleOrDefaultAsync(x => x.Id == titleId, cancellationToken);
        if (title is null) return OperationResult.Fail("Kitap kaydı bulunamadı.");
        if (title.Copies.Any(x => x.Status is BookCopyStatus.Loaned or BookCopyStatus.Reserved))
            return OperationResult.Fail("Ödünçte veya ayrılmış kopyası bulunan kitap arşivlenemez.");
        title.IsArchived = true;
        foreach (var copy in title.Copies) copy.Status = BookCopyStatus.Archived;
        AddAudit(db, ActivityType.BookArchived, actor, nameof(BookTitle), title.Id,
            new { title.Title, title.Authors, CopyCount = title.Copies.Count });
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Kitap ve kopyaları arşivlendi.");
    }

    public async Task<OperationResult> ArchiveCopyAsync(Guid copyId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var copy = await db.BookCopies.SingleOrDefaultAsync(x => x.Id == copyId, cancellationToken);
        if (copy is null) return OperationResult.Fail("Kitap kopyası bulunamadı.");
        if (copy.Status is BookCopyStatus.Loaned or BookCopyStatus.Reserved)
            return OperationResult.Fail("Ödünçte veya ayrılmış kopya arşivlenemez.");
        copy.Status = BookCopyStatus.Archived;
        AddAudit(db, ActivityType.CopyArchived, actor, nameof(BookCopy), copy.Id, copy);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("Kitap kopyası arşivlendi.");
    }

    public async Task<OperationResult> DeleteTitleAsync(Guid titleId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var title = await db.BookTitles.Include(x => x.Copies).SingleOrDefaultAsync(x => x.Id == titleId, cancellationToken);
        if (title is null) return OperationResult.Fail("Kitap kaydı bulunamadı.");
        var copyIds = title.Copies.Select(x => x.Id).ToList();
        if (await db.Loans.AnyAsync(x => copyIds.Contains(x.BookCopyId), cancellationToken) || await db.Reservations.AnyAsync(x => x.BookTitleId == titleId, cancellationToken))
            return OperationResult.Fail("Geçmişi bulunan kitap kalıcı silinemez; arşivleyin.");
        AddAudit(db, ActivityType.BookArchived, actor, nameof(BookTitle), title.Id, new { title.Title }, "İlişkisiz hatalı kayıt kalıcı silindi.");
        db.BookCopies.RemoveRange(title.Copies); db.BookTitles.Remove(title); await db.SaveChangesAsync(cancellationToken);
        return OperationResult.Ok("İlişkisiz kitap kaydı kalıcı silindi.");
    }

    public async Task<OperationResult> DeleteCopyAsync(Guid copyId, string actor, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var copy = await db.BookCopies.SingleOrDefaultAsync(x => x.Id == copyId, cancellationToken);
        if (copy is null) return OperationResult.Fail("Kitap kopyası bulunamadı.");
        if (await db.Loans.AnyAsync(x => x.BookCopyId == copyId, cancellationToken) || await db.Reservations.AnyAsync(x => x.AllocatedBookCopyId == copyId, cancellationToken))
            return OperationResult.Fail("Geçmişi bulunan kopya kalıcı silinemez; arşivleyin.");
        AddAudit(db, ActivityType.CopyArchived, actor, nameof(BookCopy), copy.Id, new { copy.Barcode }, "İlişkisiz hatalı kopya kalıcı silindi.");
        db.BookCopies.Remove(copy); await db.SaveChangesAsync(cancellationToken); return OperationResult.Ok("İlişkisiz kopya kalıcı silindi.");
    }

    internal static void AddAudit(LibraryDbContext db, ActivityType activity, string actor, string entityType, Guid? entityId, object? state, string? description = null, Guid? operatorId = null)
        => db.AuditEntries.Add(new AuditEntry
        {
            ActivityType = activity, ActorName = string.IsNullOrWhiteSpace(actor) ? "Sistem" : actor,
            EntityType = entityType, EntityId = entityId, OperatorId = operatorId,
            NewStateJson = state is null ? null : JsonSerializer.Serialize(state, state.GetType()), Description = description
        });

    private static string? ValidateTitle(BookTitleInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Title)) return "Kitap adı zorunludur.";
        if (string.IsNullOrWhiteSpace(input.Authors)) return "Yazar bilgisi zorunludur.";
        if (input.PublicationYear is < 0 or > 3000) return "Basım yılı geçerli değil.";
        return null;
    }

    private static BookTitle CreateTitle(BookTitleInput input) => new()
    {
        Title = input.Title.Trim(), Authors = input.Authors.Trim(), Isbn = Clean(input.Isbn),
        Publisher = Clean(input.Publisher), PublicationYear = input.PublicationYear,
        Category = Clean(input.Category), Language = string.IsNullOrWhiteSpace(input.Language) ? "Türkçe" : input.Language.Trim(),
        Description = Clean(input.Description)
    };

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
