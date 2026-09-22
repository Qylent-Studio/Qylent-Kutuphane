using Qylent.Kutuphane.Core.Domain;

namespace Qylent.Kutuphane.Core.Contracts;

public sealed record OperationResult(bool Success, string Message)
{
    public static OperationResult Ok(string message = "İşlem tamamlandı.") => new(true, message);
    public static OperationResult Fail(string message) => new(false, message);
}

public sealed record OperationResult<T>(bool Success, string Message, T? Value)
{
    public static OperationResult<T> Ok(T value, string message = "İşlem tamamlandı.") => new(true, message, value);
    public static OperationResult<T> Fail(string message) => new(false, message, default);
}

public sealed record SecurityQuestionInput(string Question, string Answer);

public sealed record SetupRequest(
    string LibraryName,
    string? LogoPath,
    LibraryType LibraryType,
    OperatorMode OperatorMode,
    int DefaultLoanDays,
    int DefaultMaxActiveLoans,
    int DefaultMaxRenewals,
    string BackupDirectory,
    string AdminPassword,
    IReadOnlyList<SecurityQuestionInput> SecurityQuestions,
    string? InitialOperatorName = null,
    string? InitialOperatorPin = null);

public sealed record MemberInput(
    string MemberNumber,
    string FullName,
    string MemberType,
    string? ClassOrUnit,
    string? Phone,
    string? Email,
    string? Address,
    IReadOnlyDictionary<Guid, string?>? CustomFields = null);

public sealed record MemberSummary(Guid Id, string MemberNumber, string FullName, string MemberType, string? ClassOrUnit, bool IsBlocked, bool IsArchived);
public sealed record MemberDetails(
    Guid Id,
    string MemberNumber,
    string FullName,
    string MemberType,
    string? ClassOrUnit,
    string? Phone,
    string? Email,
    string? Address,
    IReadOnlyDictionary<Guid, string?> CustomFields,
    bool IsArchived);

public sealed record BookTitleInput(
    string Title,
    string Authors,
    string? Isbn,
    string? Publisher,
    int? PublicationYear,
    string? Category,
    string Language,
    string? Description);

public sealed record BookCopyInput(Guid BookTitleId, string Barcode, string? ShelfLocation);
public sealed record BookSearchResult(Guid BookTitleId, Guid? BookCopyId, string Title, string Authors, string? Isbn, string? Barcode, string? ShelfLocation, BookCopyStatus? Status, string? Category = null, bool IsTitleArchived = false);

public sealed record CheckoutRequest(string MemberNumber, string Barcode, Guid? OperatorId = null);
public sealed record ReturnRequest(string Barcode, Guid? OperatorId = null);
public sealed record ReservationRequest(string MemberNumber, Guid BookTitleId, Guid? OperatorId = null);
public sealed record LoanSummary(Guid LoanId, string MemberNumber, string MemberName, string Barcode, string BookTitle, DateTimeOffset LoanedAtUtc, DateTimeOffset DueAtUtc, int RenewalCount, bool IsOverdue);
public sealed record ReservationSummary(Guid ReservationId, string MemberNumber, string MemberName, string BookTitle, DateTimeOffset RequestedAtUtc, ReservationStatus Status, int QueuePosition);

public sealed record AdminSession(Guid Id, DateTimeOffset ExpiresAtUtc);

public sealed record ReportRequest(
    ReportPeriodKind PeriodKind,
    DateOnly StartDate,
    DateOnly EndDate,
    IReadOnlyList<string> Columns,
    bool IncludeOverdueSheet,
    bool IncludeReservationsSheet,
    string OutputPath);

public sealed record BackupRequest(string OutputPath, string Password, bool IsAutomatic);
public sealed record RestoreRequest(string BackupPath, string Password);
public sealed record LoanRuleInput(string Name, string? MemberType, string? BookCategory, int LoanDays, int MaxActiveLoans, int MaxRenewals, int Priority);
public sealed record OperatorInput(string Name, string? Pin);
public sealed record OperatorChoice(Guid Id, string Name, bool HasPin);
public sealed record OperatorLoginState(OperatorMode Mode, IReadOnlyList<OperatorChoice> Operators);
public sealed record OperatorSession(Guid? OperatorId, string DisplayName, OperatorMode Mode);
public sealed record BookCreationResult(Guid BookTitleId, Guid BookCopyId);
public sealed record MemberFieldInput(string Name, MemberFieldType FieldType, bool IsRequired, bool IsSensitive, string? ChoiceOptions);
public sealed record AuditSummary(DateTimeOffset OccurredAtUtc, string Activity, string Actor, string Entity, string? Description);
public sealed record LibrarySettingsInput(
    string LibraryName,
    string? LogoPath,
    LibraryType LibraryType,
    OperatorMode OperatorMode,
    int DefaultLoanDays,
    int DefaultMaxActiveLoans,
    int DefaultMaxRenewals,
    string BackupDirectory,
    ThemePreference ThemePreference = ThemePreference.System);
public sealed record ReportPresetInput(
    Guid? Id,
    string Name,
    IReadOnlyList<string> Columns,
    bool IncludeOverdueSheet,
    bool IncludeReservationsSheet);
