namespace Qylent.Kutuphane.Core.Domain;

public abstract class EntityBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LibraryProfile : EntityBase
{
    public string Name { get; set; } = "Qylent Kütüphane";
    public string? LogoPath { get; set; }
    public LibraryType LibraryType { get; set; } = LibraryType.General;
    public OperatorMode OperatorMode { get; set; } = OperatorMode.Shared;
    public int DefaultLoanDays { get; set; } = 14;
    public int DefaultMaxActiveLoans { get; set; } = 5;
    public int DefaultMaxRenewals { get; set; } = 1;
    public string BackupDirectory { get; set; } = string.Empty;
    public bool SetupCompleted { get; set; }
}

public sealed class AdminCredential : EntityBase
{
    public string PasswordHash { get; set; } = string.Empty;
    public string PasswordSalt { get; set; } = string.Empty;
    public int FailedAttempts { get; set; }
    public DateTimeOffset? LockedUntilUtc { get; set; }
    public DateTimeOffset? LastLoginAtUtc { get; set; }
}

public sealed class SecurityAnswer : EntityBase
{
    public int Order { get; set; }
    public string Question { get; set; } = string.Empty;
    public string AnswerHash { get; set; } = string.Empty;
    public string AnswerSalt { get; set; } = string.Empty;
}

public sealed class Operator : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? PinHash { get; set; }
    public string? PinSalt { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Member : EntityBase
{
    public string MemberNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string MemberType { get; set; } = "Genel";
    public string? ClassOrUnit { get; set; }
    public string? EncryptedPhone { get; set; }
    public string? EncryptedEmail { get; set; }
    public string? EncryptedAddress { get; set; }
    public bool IsBlocked { get; set; }
    public string? BlockReason { get; set; }
    public bool IsArchived { get; set; }
    public List<MemberFieldValue> CustomFieldValues { get; set; } = [];
    public List<Loan> Loans { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
}

public sealed class MemberFieldDefinition : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public MemberFieldType FieldType { get; set; }
    public bool IsRequired { get; set; }
    public bool IsSensitive { get; set; }
    public bool IsEnabled { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string? ChoiceOptionsJson { get; set; }
    public List<MemberFieldValue> Values { get; set; } = [];
}

public sealed class MemberFieldValue : EntityBase
{
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid DefinitionId { get; set; }
    public MemberFieldDefinition Definition { get; set; } = null!;
    public string? Value { get; set; }
}

public sealed class BookTitle : EntityBase
{
    public string Title { get; set; } = string.Empty;
    public string Authors { get; set; } = string.Empty;
    public string? Isbn { get; set; }
    public string? Publisher { get; set; }
    public int? PublicationYear { get; set; }
    public string? Category { get; set; }
    public string Language { get; set; } = "Türkçe";
    public string? Description { get; set; }
    public bool IsArchived { get; set; }
    public List<BookCopy> Copies { get; set; } = [];
    public List<Reservation> Reservations { get; set; } = [];
}

public sealed class BookCopy : EntityBase
{
    public Guid BookTitleId { get; set; }
    public BookTitle BookTitle { get; set; } = null!;
    public string Barcode { get; set; } = string.Empty;
    public string? ShelfLocation { get; set; }
    public BookCopyStatus Status { get; set; } = BookCopyStatus.Available;
    public List<Loan> Loans { get; set; } = [];
}

public sealed class Loan : EntityBase
{
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid BookCopyId { get; set; }
    public BookCopy BookCopy { get; set; } = null!;
    public Guid? OperatorId { get; set; }
    public Operator? Operator { get; set; }
    public DateTimeOffset LoanedAtUtc { get; set; }
    public DateTimeOffset DueAtUtc { get; set; }
    public DateTimeOffset? ReturnedAtUtc { get; set; }
    public int RenewalCount { get; set; }
    public LoanStatus Status { get; set; } = LoanStatus.Active;
}

public sealed class Reservation : EntityBase
{
    public Guid MemberId { get; set; }
    public Member Member { get; set; } = null!;
    public Guid BookTitleId { get; set; }
    public BookTitle BookTitle { get; set; } = null!;
    public Guid? AllocatedBookCopyId { get; set; }
    public BookCopy? AllocatedBookCopy { get; set; }
    public DateTimeOffset RequestedAtUtc { get; set; }
    public DateTimeOffset? ReadyUntilUtc { get; set; }
    public ReservationStatus Status { get; set; } = ReservationStatus.Waiting;
}

public sealed class LoanRule : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string? MemberType { get; set; }
    public string? BookCategory { get; set; }
    public int LoanDays { get; set; }
    public int MaxActiveLoans { get; set; }
    public int MaxRenewals { get; set; }
    public int Priority { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ReportPreset : EntityBase
{
    public string Name { get; set; } = string.Empty;
    public string ColumnsJson { get; set; } = "[]";
    public bool IncludeOverdueSheet { get; set; }
    public bool IncludeReservationsSheet { get; set; }
}

public sealed class AuditEntry : EntityBase
{
    public ActivityType ActivityType { get; set; }
    public Guid? OperatorId { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid? EntityId { get; set; }
    public string? OldStateJson { get; set; }
    public string? NewStateJson { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class BackupRecord : EntityBase
{
    public string FilePath { get; set; } = string.Empty;
    public bool IsAutomatic { get; set; }
    public long SizeBytes { get; set; }
    public string Sha256 { get; set; } = string.Empty;
    public bool IsSuccessful { get; set; }
}

public sealed class SchemaInfo
{
    public int Id { get; set; } = 1;
    public int Version { get; set; } = 1;
    public DateTimeOffset AppliedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

