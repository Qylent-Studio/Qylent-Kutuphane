using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class CirculationService(IDbContextFactory<LibraryDbContext> contextFactory) : ICirculationService
{
    public async Task<OperationResult<LoanSummary>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.Members.SingleOrDefaultAsync(x => x.MemberNumber == request.MemberNumber.Trim(), cancellationToken);
        if (member is null || member.IsArchived) return OperationResult<LoanSummary>.Fail("Aktif üye bulunamadı.");
        if (member.IsBlocked) return OperationResult<LoanSummary>.Fail($"Üye engelli: {member.BlockReason ?? "neden belirtilmedi"}");
        var copy = await db.BookCopies.Include(x => x.BookTitle).SingleOrDefaultAsync(x => x.Barcode == request.Barcode.Trim(), cancellationToken);
        if (copy is null || copy.Status == BookCopyStatus.Archived || copy.BookTitle.IsArchived)
            return OperationResult<LoanSummary>.Fail("Aktif kitap kopyası bulunamadı.");

        var readyReservation = await db.Reservations.OrderBy(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(x => x.AllocatedBookCopyId == copy.Id && x.Status == ReservationStatus.Ready, cancellationToken);
        if (copy.Status == BookCopyStatus.Reserved && readyReservation?.MemberId != member.Id)
            return OperationResult<LoanSummary>.Fail("Bu kopya başka bir üye için ayrılmış.");
        if (copy.Status is not (BookCopyStatus.Available or BookCopyStatus.Reserved))
            return OperationResult<LoanSummary>.Fail("Kitap kopyası ödünç verilebilir durumda değil.");

        var rule = await ResolveRuleAsync(db, member, copy.BookTitle, cancellationToken);
        var activeCount = await db.Loans.CountAsync(x => x.MemberId == member.Id && x.Status == LoanStatus.Active, cancellationToken);
        if (activeCount >= rule.MaxActiveLoans) return OperationResult<LoanSummary>.Fail("Üyenin aktif kitap sınırı dolu.");
        var now = DateTimeOffset.UtcNow;
        var loan = new Loan
        {
            MemberId = member.Id, BookCopyId = copy.Id, OperatorId = request.OperatorId,
            LoanedAtUtc = now, DueAtUtc = now.AddDays(rule.LoanDays), Status = LoanStatus.Active
        };
        copy.Status = BookCopyStatus.Loaned;
        if (readyReservation is not null) readyReservation.Status = ReservationStatus.Completed;
        db.Loans.Add(loan);
        var actor = await ActorNameAsync(db, request.OperatorId, cancellationToken);
        CatalogService.AddAudit(db, ActivityType.Loaned, actor, nameof(Loan), loan.Id,
            new { member.MemberNumber, copy.Barcode, loan.LoanedAtUtc, loan.DueAtUtc }, operatorId: request.OperatorId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<LoanSummary>.Ok(ToSummary(loan, member, copy), $"{copy.BookTitle.Title} ödünç verildi.");
    }

    public async Task<OperationResult<LoanSummary>> ReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var loan = await db.Loans.Include(x => x.Member).Include(x => x.BookCopy).ThenInclude(x => x.BookTitle)
            .SingleOrDefaultAsync(x => x.BookCopy.Barcode == request.Barcode.Trim() && x.Status == LoanStatus.Active, cancellationToken);
        if (loan is null) return OperationResult<LoanSummary>.Fail("Bu barkoda ait aktif ödünç bulunamadı.");
        loan.Status = LoanStatus.Returned;
        loan.ReturnedAtUtc = DateTimeOffset.UtcNow;
        var next = await db.Reservations.OrderBy(x => x.RequestedAtUtc)
            .FirstOrDefaultAsync(x => x.BookTitleId == loan.BookCopy.BookTitleId && x.Status == ReservationStatus.Waiting, cancellationToken);
        if (next is null)
        {
            loan.BookCopy.Status = BookCopyStatus.Available;
        }
        else
        {
            next.Status = ReservationStatus.Ready;
            next.AllocatedBookCopyId = loan.BookCopyId;
            next.ReadyUntilUtc = DateTimeOffset.UtcNow.AddDays(3);
            loan.BookCopy.Status = BookCopyStatus.Reserved;
        }
        var actor = await ActorNameAsync(db, request.OperatorId, cancellationToken);
        CatalogService.AddAudit(db, ActivityType.Returned, actor, nameof(Loan), loan.Id,
            new { loan.Member.MemberNumber, loan.BookCopy.Barcode, loan.ReturnedAtUtc }, operatorId: request.OperatorId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult<LoanSummary>.Ok(ToSummary(loan, loan.Member, loan.BookCopy), "Kitap iade alındı.");
    }

    public async Task<OperationResult<LoanSummary>> RenewAsync(Guid loanId, Guid? operatorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var loan = await db.Loans.Include(x => x.Member).Include(x => x.BookCopy).ThenInclude(x => x.BookTitle)
            .SingleOrDefaultAsync(x => x.Id == loanId, cancellationToken);
        if (loan is null || loan.Status != LoanStatus.Active) return OperationResult<LoanSummary>.Fail("Aktif ödünç bulunamadı.");
        if (loan.Member.IsBlocked) return OperationResult<LoanSummary>.Fail("Engelli üyenin ödüncü uzatılamaz.");
        if (await db.Reservations.AnyAsync(x => x.BookTitleId == loan.BookCopy.BookTitleId && x.MemberId != loan.MemberId && x.Status == ReservationStatus.Waiting, cancellationToken))
            return OperationResult<LoanSummary>.Fail("Kitap başka bir üye tarafından ayırtılmış.");
        var rule = await ResolveRuleAsync(db, loan.Member, loan.BookCopy.BookTitle, cancellationToken);
        if (loan.RenewalCount >= rule.MaxRenewals) return OperationResult<LoanSummary>.Fail("Uzatma sınırı dolmuş.");
        loan.RenewalCount++;
        loan.DueAtUtc = (loan.DueAtUtc > DateTimeOffset.UtcNow ? loan.DueAtUtc : DateTimeOffset.UtcNow).AddDays(rule.LoanDays);
        var actor = await ActorNameAsync(db, operatorId, cancellationToken);
        CatalogService.AddAudit(db, ActivityType.Renewed, actor, nameof(Loan), loan.Id,
            new { loan.RenewalCount, loan.DueAtUtc }, operatorId: operatorId);
        await db.SaveChangesAsync(cancellationToken);
        return OperationResult<LoanSummary>.Ok(ToSummary(loan, loan.Member, loan.BookCopy), "Ödünç süresi uzatıldı.");
    }

    public async Task<OperationResult<ReservationSummary>> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var member = await db.Members.SingleOrDefaultAsync(x => x.MemberNumber == request.MemberNumber.Trim(), cancellationToken);
        if (member is null || member.IsArchived || member.IsBlocked) return OperationResult<ReservationSummary>.Fail("Üye ayırtma yapamaz.");
        var title = await db.BookTitles.SingleOrDefaultAsync(x => x.Id == request.BookTitleId && !x.IsArchived, cancellationToken);
        if (title is null) return OperationResult<ReservationSummary>.Fail("Kitap bulunamadı.");
        if (await db.Reservations.AnyAsync(x => x.MemberId == member.Id && x.BookTitleId == title.Id && (x.Status == ReservationStatus.Waiting || x.Status == ReservationStatus.Ready), cancellationToken))
            return OperationResult<ReservationSummary>.Fail("Üyenin bu kitap için aktif ayırtması var.");
        var available = await db.BookCopies.OrderBy(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(x => x.BookTitleId == title.Id && x.Status == BookCopyStatus.Available, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var reservation = new Reservation
        {
            MemberId = member.Id, BookTitleId = title.Id, RequestedAtUtc = now,
            Status = available is null ? ReservationStatus.Waiting : ReservationStatus.Ready,
            AllocatedBookCopyId = available?.Id, ReadyUntilUtc = available is null ? null : now.AddDays(3)
        };
        if (available is not null) available.Status = BookCopyStatus.Reserved;
        db.Reservations.Add(reservation);
        var actor = await ActorNameAsync(db, request.OperatorId, cancellationToken);
        CatalogService.AddAudit(db, ActivityType.Reserved, actor, nameof(Reservation), reservation.Id,
            new { member.MemberNumber, title.Title, reservation.Status }, operatorId: request.OperatorId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var position = reservation.Status == ReservationStatus.Ready ? 1 :
            await db.Reservations.CountAsync(x => x.BookTitleId == title.Id && x.Status == ReservationStatus.Waiting && x.RequestedAtUtc <= reservation.RequestedAtUtc, cancellationToken);
        return OperationResult<ReservationSummary>.Ok(new(reservation.Id, member.MemberNumber, member.FullName, title.Title, now, reservation.Status, position), "Kitap ayırtıldı.");
    }

    public async Task<OperationResult> CancelReservationAsync(Guid reservationId, Guid? operatorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var reservation = await db.Reservations.Include(x => x.Member).Include(x => x.BookTitle).Include(x => x.AllocatedBookCopy)
            .SingleOrDefaultAsync(x => x.Id == reservationId, cancellationToken);
        if (reservation is null || reservation.Status is not (ReservationStatus.Waiting or ReservationStatus.Ready))
            return OperationResult.Fail("Aktif ayırtma bulunamadı.");
        reservation.Status = ReservationStatus.Cancelled;
        if (reservation.AllocatedBookCopy is not null)
        {
            var next = await db.Reservations.OrderBy(x => x.RequestedAtUtc)
                .FirstOrDefaultAsync(x => x.BookTitleId == reservation.BookTitleId && x.Id != reservation.Id && x.Status == ReservationStatus.Waiting, cancellationToken);
            if (next is null) reservation.AllocatedBookCopy.Status = BookCopyStatus.Available;
            else
            {
                next.Status = ReservationStatus.Ready;
                next.AllocatedBookCopyId = reservation.AllocatedBookCopyId;
                next.ReadyUntilUtc = DateTimeOffset.UtcNow.AddDays(3);
            }
        }
        var actor = await ActorNameAsync(db, operatorId, cancellationToken);
        CatalogService.AddAudit(db, ActivityType.ReservationCancelled, actor, nameof(Reservation), reservation.Id,
            new { reservation.Member.MemberNumber, reservation.BookTitle.Title }, operatorId: operatorId);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return OperationResult.Ok("Ayırtma iptal edildi.");
    }

    public async Task<IReadOnlyList<LoanSummary>> GetActiveLoansAsync(bool overdueOnly = false, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var query = db.Loans.AsNoTracking().Include(x => x.Member).Include(x => x.BookCopy).ThenInclude(x => x.BookTitle)
            .Where(x => x.Status == LoanStatus.Active);
        if (overdueOnly) query = query.Where(x => x.DueAtUtc < now);
        var loans = await query.OrderBy(x => x.DueAtUtc).Take(1000).ToListAsync(cancellationToken);
        return loans.Select(x => ToSummary(x, x.Member, x.BookCopy)).ToList();
    }

    public async Task<IReadOnlyList<ReservationSummary>> GetReservationsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var reservations = await db.Reservations.AsNoTracking().Include(x => x.Member).Include(x => x.BookTitle)
            .Where(x => x.Status == ReservationStatus.Waiting || x.Status == ReservationStatus.Ready)
            .OrderBy(x => x.BookTitle.Title).ThenBy(x => x.RequestedAtUtc).Take(1000).ToListAsync(cancellationToken);
        return reservations.GroupBy(x => x.BookTitleId).SelectMany(group => group.Select((x, index) =>
            new ReservationSummary(x.Id, x.Member.MemberNumber, x.Member.FullName, x.BookTitle.Title, x.RequestedAtUtc, x.Status, index + 1))).ToList();
    }

    private static async Task<(int LoanDays, int MaxActiveLoans, int MaxRenewals)> ResolveRuleAsync(LibraryDbContext db, Member member, BookTitle title, CancellationToken cancellationToken)
    {
        var specific = await db.LoanRules.AsNoTracking().Where(x => x.IsActive &&
                (x.MemberType == null || x.MemberType == member.MemberType) &&
                (x.BookCategory == null || x.BookCategory == title.Category))
            .OrderByDescending(x => x.Priority).FirstOrDefaultAsync(cancellationToken);
        if (specific is not null) return (specific.LoanDays, specific.MaxActiveLoans, specific.MaxRenewals);
        var profile = await db.LibraryProfiles.AsNoTracking().SingleAsync(x => x.SetupCompleted, cancellationToken);
        return (profile.DefaultLoanDays, profile.DefaultMaxActiveLoans, profile.DefaultMaxRenewals);
    }

    private static async Task<string> ActorNameAsync(LibraryDbContext db, Guid? operatorId, CancellationToken cancellationToken)
        => operatorId is null ? "Ortak Görevli" :
            await db.Operators.Where(x => x.Id == operatorId).Select(x => x.Name).SingleOrDefaultAsync(cancellationToken) ?? "Bilinmeyen Görevli";

    private static LoanSummary ToSummary(Loan loan, Member member, BookCopy copy) =>
        new(loan.Id, member.MemberNumber, member.FullName, copy.Barcode, copy.BookTitle.Title,
            loan.LoanedAtUtc, loan.DueAtUtc, loan.RenewalCount, loan.Status == LoanStatus.Active && loan.DueAtUtc < DateTimeOffset.UtcNow);
}

