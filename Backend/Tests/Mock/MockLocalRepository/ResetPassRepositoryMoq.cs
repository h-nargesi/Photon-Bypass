using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class ResetPassRepositoryMoq : Mock<IResetPassRepository>, IUnitLevelService
{
    private readonly Dictionary<string, ResetPassEntity> data = [];

    public event Action<ResetPassEntity>? OnAddHashCode;

    public event Action<string, ResetPassEntity?>? OnGetAccount;

    public event Action<int>? OnExpireActiveCodes;

    public ResetPassRepositoryMoq()
    {
        Setup(x => x.AddHashCode(It.IsNotNull<ResetPassEntity>()))
            .Returns<ResetPassEntity>(hash_code =>
            {
                data[hash_code.HashCode] = hash_code;
                OnAddHashCode?.Invoke(hash_code);
                return Task.CompletedTask;
            });

        Setup(x => x.GetAccount(It.IsNotNull<string>()))
            .Returns<string>(hash_code =>
            {
                if (!data.TryGetValue(hash_code, out var entity))
                {
                    entity = null;
                }

                OnGetAccount?.Invoke(hash_code, entity);

                return Task.FromResult(entity);
            });

        Setup(x => x.ExpireActiveCodes(It.IsInRange(1, int.MaxValue, Moq.Range.Inclusive)))
            .Returns<int>(account_id =>
            {
                var now = DateTime.Now;

                foreach (var entity in data.Values.Where(entity => entity.AccountId == account_id))
                {
                    if (entity.ExpireDate > now)
                    {
                        entity.ExpireDate = now;
                    }
                }

                OnExpireActiveCodes?.Invoke(account_id);
                return Task.CompletedTask;
            });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<ResetPassRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<ResetPassRepositoryMoq>().Object);
    }
}