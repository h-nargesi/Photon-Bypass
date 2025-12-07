using Moq;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using System.Net.Mail;

namespace PhotonBypass.Test.ServerBridge;

internal class IEmailHandlerMoq : Mock<IEmailHandler>, IOutSourceMoq
{
    public event Action<MailMessage>? OnSend;

    private IEmailHandlerMoq()
    {
        Setup(x => x.Send(It.IsAny<MailMessage>()))
            .Returns<MailMessage>(mail =>
            {
                OnSend?.Invoke(mail);
                return Task.CompletedTask;
            });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<IEmailHandlerMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<IEmailHandlerMoq>().Object);
    }
}
