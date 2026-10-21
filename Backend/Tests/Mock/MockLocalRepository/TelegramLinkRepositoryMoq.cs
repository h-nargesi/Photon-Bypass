using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

internal class TelegramLinkRepositoryMoq : Mock<ITelegramLinkRepository>, IUnitLevelService
{
    private readonly Dictionary<string, TelegramLinkEntity> data = [];

    public event Action<TelegramLinkEntity>? OnAddCode;

    public event Action<string, TelegramLinkEntity?>? OnConsume;

    public event Action<int>? OnExpireActiveCodes;

    public TelegramLinkRepositoryMoq()
    {
        Setup(x => x.AddCode(It.IsNotNull<TelegramLinkEntity>()))
            .Returns<TelegramLinkEntity>(code =>
            {
                data[code.Code] = code;
                OnAddCode?.Invoke(code);
                return Task.CompletedTask;
            });

        Setup(x => x.Consume(It.IsNotNull<string>()))
            .Returns<string>(code =>
            {
                if (!data.TryGetValue(code, out var entity))
                {
                    entity = null;
                }
                else
                {
                    // delete-output semantics of the real repository
                    data.Remove(code);
                }

                OnConsume?.Invoke(code, entity);

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
        services.AddScoped<TelegramLinkRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<TelegramLinkRepositoryMoq>().Object);
    }
}
