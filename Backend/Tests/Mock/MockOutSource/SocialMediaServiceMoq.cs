using Moq;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockOutSource;

public class SocialMediaServiceMoq : Mock<ISocialMediaService>, IUnitLevelService
{
    public event Action<IReadOnlyList<string>>? OnAlarmServerCapacity;

    public event Action<PanelUserEntity, string>? OnSendResetPasswordLink;

    public event Action<PanelUserEntity>? OnFinishServiceAlert;

    public event Action<PanelUserEntity>? OnInvalidPasswordAlert;

    public event Action<PanelUserEntity>? OnNewUserRegistrationAlert;

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

        Setup(service => service.Supports(It.IsAny<PanelUserEntity>()))
            .Returns<PanelUserEntity>(account => SupportsAccounts && account.TelegramChatId != null);

        Setup(service => service.SendResetPasswordLink(It.IsAny<PanelUserEntity>(), It.IsAny<string>()))
            .Returns<PanelUserEntity, string>((account, hash_code) =>
            {
                OnSendResetPasswordLink?.Invoke(account, hash_code);

                return Task.CompletedTask;
            });

        Setup(service => service.FinishServiceAlert(It.IsAny<PanelUserEntity>(), It.IsAny<string>(), It.IsAny<string>()))
            .Callback<PanelUserEntity, string, string>((account, _, _) => OnFinishServiceAlert?.Invoke(account))
            .Returns(Task.CompletedTask);

        Setup(service => service.InvalidPasswordAlert(It.IsAny<PanelUserEntity>()))
            .Callback<PanelUserEntity>(account => OnInvalidPasswordAlert?.Invoke(account))
            .Returns(Task.CompletedTask);

        Setup(service => service.NewUserRegistrationAlert(It.IsAny<PanelUserEntity>()))
            .Callback<PanelUserEntity>(account => OnNewUserRegistrationAlert?.Invoke(account))
            .Returns(Task.CompletedTask);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<SocialMediaServiceMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<SocialMediaServiceMoq>().Object);
    }
}
