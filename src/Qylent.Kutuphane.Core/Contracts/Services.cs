using Qylent.Kutuphane.Core.Domain;

namespace Qylent.Kutuphane.Core.Contracts;

public interface ISetupService
{
    Task<bool> IsSetupCompletedAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> CompleteSetupAsync(SetupRequest request, CancellationToken cancellationToken = default);
}

public interface ISecurityService
{
    Task<OperationResult<AdminSession>> LoginAdminAsync(string password, CancellationToken cancellationToken = default);
    bool IsAdminSessionValid(AdminSession? session);
    Task<OperationResult> ResetAdminPasswordAsync(IReadOnlyList<string> answers, string newPassword, CancellationToken cancellationToken = default);
    string EncryptSensitive(string plainText);
    string DecryptSensitive(string cipherText);
}

public interface ICatalogService
{
    Task<OperationResult<BookCreationResult>> AddTitleWithFirstCopyAsync(BookTitleInput title, string barcode, string? shelfLocation, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult<BookTitle>> AddTitleAsync(BookTitleInput input, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult<BookCopy>> AddCopyAsync(BookCopyInput input, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateTitleAsync(Guid titleId, BookTitleInput input, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateCopyAsync(Guid copyId, string barcode, string? shelfLocation, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BookSearchResult>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<OperationResult> ArchiveTitleAsync(Guid titleId, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> ArchiveCopyAsync(Guid copyId, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteTitleAsync(Guid titleId, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteCopyAsync(Guid copyId, string actor, CancellationToken cancellationToken = default);
}

public interface IOperatorSessionService
{
    OperatorSession? Current { get; }
    Task<OperatorLoginState> GetLoginStateAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<OperatorSession>> LoginAsync(Guid? operatorId, string? pin, CancellationToken cancellationToken = default);
}

public interface IMemberService
{
    Task<OperationResult<Member>> AddAsync(MemberInput input, string actor, CancellationToken cancellationToken = default);
    Task<MemberDetails?> GetAsync(Guid memberId, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateAsync(Guid memberId, MemberInput input, string actor, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberSummary>> SearchAsync(string query, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<OperationResult> ArchiveAsync(Guid memberId, string actor, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteAsync(Guid memberId, string actor, CancellationToken cancellationToken = default);
}

public interface ICirculationService
{
    Task<OperationResult<LoanSummary>> CheckoutAsync(CheckoutRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult<LoanSummary>> ReturnAsync(ReturnRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult<LoanSummary>> RenewAsync(Guid loanId, Guid? operatorId = null, CancellationToken cancellationToken = default);
    Task<OperationResult<ReservationSummary>> ReserveAsync(ReservationRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> CancelReservationAsync(Guid reservationId, Guid? operatorId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanSummary>> GetActiveLoansAsync(bool overdueOnly = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReservationSummary>> GetReservationsAsync(CancellationToken cancellationToken = default);
}

public interface IReportService
{
    Task<OperationResult<string>> ExportAsync(ReportRequest request, CancellationToken cancellationToken = default);
}

public interface IBackupService
{
    Task<OperationResult<string>> CreateAsync(BackupRequest request, CancellationToken cancellationToken = default);
    Task<OperationResult> RestoreAsync(RestoreRequest request, CancellationToken cancellationToken = default);
    Task PruneAutomaticBackupsAsync(int keepCount = 30, CancellationToken cancellationToken = default);
}

public interface IAdministrationService
{
    Task<LibraryProfile?> GetLibraryProfileAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateLibrarySettingsAsync(LibrarySettingsInput input, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanRule>> GetLoanRulesAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddLoanRuleAsync(LoanRuleInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateLoanRuleAsync(Guid id, LoanRuleInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> SetLoanRuleActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteLoanRuleAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Operator>> GetOperatorsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddOperatorAsync(OperatorInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateOperatorAsync(Guid id, OperatorInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> SetOperatorActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteOperatorAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MemberFieldDefinition>> GetMemberFieldsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult> AddMemberFieldAsync(MemberFieldInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> UpdateMemberFieldAsync(Guid id, MemberFieldInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> SetMemberFieldEnabledAsync(Guid id, bool isEnabled, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteMemberFieldAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReportPreset>> GetReportPresetsAsync(CancellationToken cancellationToken = default);
    Task<OperationResult<ReportPreset>> SaveReportPresetAsync(ReportPresetInput input, CancellationToken cancellationToken = default);
    Task<OperationResult> DeleteReportPresetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<AuditSummary>> GetAuditAsync(int take = 500, CancellationToken cancellationToken = default);
}
