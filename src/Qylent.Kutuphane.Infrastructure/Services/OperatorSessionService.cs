using Microsoft.EntityFrameworkCore;
using Qylent.Kutuphane.Core.Contracts;
using Qylent.Kutuphane.Core.Domain;
using Qylent.Kutuphane.Infrastructure.Persistence;
using Qylent.Kutuphane.Infrastructure.Security;

namespace Qylent.Kutuphane.Infrastructure.Services;

public sealed class OperatorSessionService(IDbContextFactory<LibraryDbContext> contextFactory) : IOperatorSessionService
{
    public OperatorSession? Current { get; private set; }

    public async Task<OperatorLoginState> GetLoginStateAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var mode = await db.LibraryProfiles.AsNoTracking()
            .Where(x => x.SetupCompleted).Select(x => x.OperatorMode).SingleAsync(cancellationToken);
        var operators = mode == OperatorMode.Shared
            ? []
            : await db.Operators.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.Name)
                .Select(x => new OperatorChoice(x.Id, x.Name, x.PinHash != null && x.PinSalt != null))
                .ToListAsync(cancellationToken);
        return new(mode, operators);
    }

    public async Task<OperationResult<OperatorSession>> LoginAsync(Guid? operatorId, string? pin, CancellationToken cancellationToken = default)
    {
        var state = await GetLoginStateAsync(cancellationToken);
        if (state.Mode == OperatorMode.Shared)
        {
            Current = new(null, "Ortak Görevli", state.Mode);
            return OperationResult<OperatorSession>.Ok(Current, "Ortak görevli oturumu açıldı.");
        }
        if (operatorId is null) return OperationResult<OperatorSession>.Fail("Bir görevli seçin.");

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var item = await db.Operators.AsNoTracking().SingleOrDefaultAsync(x => x.Id == operatorId && x.IsActive, cancellationToken);
        if (item is null) return OperationResult<OperatorSession>.Fail("Aktif görevli bulunamadı.");
        if (state.Mode == OperatorMode.Pin)
        {
            if (item.PinHash is null || item.PinSalt is null)
                return OperationResult<OperatorSession>.Fail("Bu görevli için PIN tanımlanmamış.");
            if (string.IsNullOrEmpty(pin) || !PasswordHasher.Verify(pin, item.PinHash, item.PinSalt))
                return OperationResult<OperatorSession>.Fail("PIN hatalı.");
        }

        Current = new(item.Id, item.Name, state.Mode);
        return OperationResult<OperatorSession>.Ok(Current, $"{item.Name} olarak giriş yapıldı.");
    }
}
