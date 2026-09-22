namespace Qylent.Kutuphane.Core.Domain;

public enum LibraryType { School, Public, PrivateInstitution, General }
public enum ThemePreference { Light, Dark, System }
public enum OperatorMode { Shared, NameSelection, Pin }
public enum BookCopyStatus { Available, Loaned, Reserved, Lost, Maintenance, Archived }
public enum LoanStatus { Active, Returned, Lost }
public enum ReservationStatus { Waiting, Ready, Completed, Cancelled, Expired }
public enum MemberFieldType { Text, Number, Date, Choice, Boolean }
public enum ReportPeriodKind { Daily, Weekly, Monthly, Custom }
public enum ActivityType
{
    SetupCompleted, MemberCreated, MemberUpdated, MemberArchived,
    BookCreated, BookUpdated, BookArchived, CopyCreated, CopyUpdated, CopyArchived,
    Loaned, Returned, Renewed, Reserved, ReservationCancelled,
    RuleCreated, OperatorCreated, MemberFieldCreated,
    ReportExported, BackupCreated, BackupRestored, AdminLogin, AdminPasswordReset,
    SettingsUpdated, ReportPresetSaved, ReportPresetDeleted,
    RuleUpdated, RuleDeleted, OperatorUpdated, OperatorDeleted, MemberFieldUpdated, MemberFieldDeleted
}
