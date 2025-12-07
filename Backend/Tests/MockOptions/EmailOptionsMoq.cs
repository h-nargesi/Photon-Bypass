using Microsoft.Extensions.Options;
using Moq;
using PhotonBypass.ServerBridge.Email;

namespace PhotonBypass.Test.MockOptions;

internal class EmailOptionsMoq : Mock<IOptions<EmailOptions>>, IOutSourceMoq
{
    public EmailOptionsMoq()
    {
        Setup(options => options.Value).Returns(new EmailOptions
        {
            Address = "info@website.com",
            FullName = "Administrator",
            Password = "password",
        });
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton(new EmailOptionsMoq().Object);
    }
}