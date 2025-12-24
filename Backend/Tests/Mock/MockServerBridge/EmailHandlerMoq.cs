using System.Net.Mail;
using Moq;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Test.Mock.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockServerBridge;

internal class EmailHandlerMoq : Mock<IEmailHandler>, IUnitLevelService, IProgramLevelService
{
    public event Action<MailMessage>? OnSend;

    public EmailHandlerMoq()
    {
        Setup(x => x.Send(It.IsAny<MailMessage>()))
            .Returns<MailMessage>(mail =>
            {
                OnSend?.Invoke(mail);
                return Task.CompletedTask;
            });

        Setup(handler => handler.Options).Returns(() => new EmailOptionsMoq().Object.Value);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<EmailHandlerMoq>();
        services.AddSingleton(s => s.GetRequiredService<EmailHandlerMoq>().Object);
    }
}
