using Moq;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockOutSource;

public class SocialMediaServiceMoq : Mock<ISocialMediaService>, IUnitLevelService
{
    public event Action<IReadOnlyList<string>>? OnAlarmServerCapacity;

    public event Action<AccountEntity, string>? OnSendResetPasswordLink;

    public event Action<AccountEntity>? OnFinishServiceAlert;

    public event Action<AccountEntity>? OnInvalidPasswordAlert;

    public event Action<AccountEntity>? OnNewUserRegistrationAlert;

    /// <summary>
    /// Channel switch for <c>Supports</c> — false (the no-op channel) by
    /// default so every legacy test keeps the email fallback. When true the
    /// answer follows the connected chat, exactly like the Telegram adapter.
    /// </summary>
    public bool SupportsAccounts { get; set; }

    public SocialMediaServiceMoq()
    {
        Setup(service => service.AlarmServerCapacity(It.IsAny<IReadOnlyList<string>>()))
            .Returns<IReadOnlyList<string>>(alarms =>
            {
                OnAlarmServerCapacity?.Invoke(alarms);

                return Task.CompletedTask;
            });

        Setup(service => service.Supports(It.IsAny<AccountEntity>()))
            .Returns<AccountEntity>(account => SupportsAccounts && account.TelegramChatId != null);

        Setup(service => service.SendResetPasswordLink(It.IsAny<AccountEntity>(), It.IsAny<string>()))
            .Returns<AccountEntity, string>((account, hash_code) =>
            {
                OnSendResetPasswordLink?.Invoke(account, hash_code);

                return Task.CompletedTask;
            });

        Setup(service => service.FinishServiceAlert(It.IsAny<AccountEntity>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<AccountEntity, string, string>((account, _, _) => OnFinishServiceAlert?.Invoke(account))
            .Returns(Task.CompletedTask);

        Setup(service => service.InvalidPasswordAlert(It.IsAny<AccountEntity>()))
            .Callback<AccountEntity>(account => OnInvalidPasswordAlert?.Invoke(account))
            .Returns(Task.CompletedTask);

        Setup(service => service.NewUserRegistrationAlert(It.IsAny<AccountEntity>()))
            .Callback<AccountEntity>(account => OnNewUserRegistrationAlert?.Invoke(account))
            .Returns(Task.CompletedTask);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<SocialMediaServiceMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<SocialMediaServiceMoq>().Object);
    }
}
