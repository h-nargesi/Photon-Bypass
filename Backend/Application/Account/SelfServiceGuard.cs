using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;

namespace PhotonBypass.Application.Account;

/// <summary>
/// Single-point guard for the self-service financial endpoints (docs/20 D4/D6):
/// the paying actor is always the logged-in account, and a dependent account
/// (no <see cref="UserTypes.AllowSelfService"/>) is refused its own
/// wallet/renewal operations with 403. Acting on behalf of another user is not
/// restricted here — target access is already validated by the AccessService
/// during LoadJobContext.
/// </summary>
public interface ISelfServiceGuard
{
    /// <summary>Throws UserException(403) when the actor is a dependent user in self view.</summary>
    Task EnsureSelfService();

    /// <summary>EnsureSelfService semantics plus resolution of the paying actor account (D4).</summary>
    Task<AccountEntity> ResolvePayingActor();

    /// <summary>
    /// Resolves the logged-in active account without any self-service check —
    /// for role-based decisions that are independent of the dependent flag
    /// (e.g. wallet history access, docs/20 phase 3).
    /// </summary>
    Task<AccountEntity> ResolveActor();
}

class SelfServiceGuard(
    Lazy<IAccountRepository> account_repo,
    Lazy<IJobContext> job_context) : ISelfServiceGuard
{
    private Lazy<IAccountRepository> AccountRepo { get; } = account_repo;
    private Lazy<IJobContext> JobContext { get; } = job_context;

    public async Task EnsureSelfService()
    {
        var context = JobContext.Value;

        if (context.Target != context.Username) return;

        var actor = await GetActor(context.Username);

        if (!actor.UserType.HasFlag(UserTypes.AllowSelfService))
        {
            throw CreateForbidden(actor.Username);
        }
    }

    public async Task<AccountEntity> ResolvePayingActor()
    {
        var context = JobContext.Value;

        var actor = await ResolveActor();

        if (context.Target == actor.Username && !actor.UserType.HasFlag(UserTypes.AllowSelfService))
        {
            throw CreateForbidden(actor.Username);
        }

        return actor;
    }

    public Task<AccountEntity> ResolveActor()
    {
        return GetActor(JobContext.Value.Username);
    }

    private async Task<AccountEntity> GetActor(string username)
    {
        var actor = await AccountRepo.Value.GetAccount(username)
                    ?? throw new UserException("کاربر پیدا نشد!", $"Account not found. actor:{username}");

        if (!actor.IsActive)
        {
            throw new UserException("کاربر غیرفعال است!", $"account is inactive: actor={actor.Username}");
        }

        return actor;
    }

    private static UserException CreateForbidden(string actor)
    {
        return new UserException("دسترسی به امکانات مالی برای این حساب فعال نیست!",
            $"Dependent account cannot run its own wallet/renewal operations (docs/20 D6): actor={actor}",
            403);
    }
}
