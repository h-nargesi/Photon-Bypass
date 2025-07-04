using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application.OutSources;

class ResetPassRepositoryMoq : Mock<IResetPassRepository>, IOutSourceMoq
{
    readonly Dictionary<string, ResetPassEntity> data = [];

    public event Action<ResetPassEntity>? OnAddHashCode;

    public event Action<string, ResetPassEntity?>? OnGetAccount;

    public void Setup()
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
    }

    public static ResetPassRepositoryMoq CreateInstance(IServiceCollection services)
    {
        var moq = new ResetPassRepositoryMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}
