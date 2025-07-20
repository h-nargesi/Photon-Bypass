using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class EmailServiceMoq : Mock<IEmailService>, IOutSourceMoq
{
    public event Action<string, string, string>? OnSendResetPasswordLink;

    public event Action<string, string, CertEmailContext>? OnSendCertEmail;

    public event Action<string, string, string, PlanType, string>? OnFinishServiceAlert;

    public EmailServiceMoq Setup()
    {
        Setup(x => x.SendResetPasswordLink(It.IsNotNull<string>(), It.IsNotNull<string>(), It.IsNotNull<string>()))
            .Returns<string, string, string>((fullname, email, hash_code) =>
            {
                OnSendResetPasswordLink?.Invoke(fullname, email, hash_code);
                return Task.CompletedTask;
            });

        Setup(x => x.SendCertEmail(It.IsNotNull<string>(), It.IsNotNull<string>(), It.IsNotNull<CertEmailContext>()))
            .Returns<string, string, CertEmailContext>((fullname, email, context) =>
            {
                OnSendCertEmail?.Invoke(fullname, email, context);
                return Task.CompletedTask;
            });

        Setup(x => x.FinishServiceAlert(It.IsNotNull<string>(), It.IsNotNull<string>(), It.IsNotNull<string>(), It.IsNotNull<PlanType>(), It.IsNotNull<string>()))
            .Returns<string, string, string, PlanType, string>((fullname, username, email, type, left) =>
            {
                OnFinishServiceAlert?.Invoke(fullname, username, email, type, left);
                return Task.CompletedTask;
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup();
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new EmailServiceMoq().Setup());
        services.AddLazyScoped(s => s.GetRequiredService<EmailServiceMoq>().Object);
    }
}
