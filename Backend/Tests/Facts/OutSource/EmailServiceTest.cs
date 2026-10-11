using PhotonBypass.Domain.OutSource;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.OutSource;

public class EmailServiceTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task SendResetPasswordLink_TemplateCachedAcrossInstances()
    {
        using var scope_1 = App.Services.CreateScope();
        using var scope_2 = App.Services.CreateScope();

        var email_service_1 = scope_1.ServiceProvider.GetRequiredService<IEmailService>();
        var email_service_2 = scope_2.ServiceProvider.GetRequiredService<IEmailService>();
        var email_handler_moq = scope_1.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var bodies = new List<string>();
        email_handler_moq.OnSend += mail => bodies.Add(mail.Body);

        // Two different service instances share the static template cache: the
        // second send must reuse the cached template and still apply its own
        // placeholder values.
        await email_service_1.SendResetPasswordLink("User One", "one@test.com", "code-111");
        await email_service_2.SendResetPasswordLink("User Two", "two@test.com", "code-222");

        Assert.Equal(2, bodies.Count);
        Assert.Contains("code-111", bodies[0]);
        Assert.Contains("code-222", bodies[1]);
    }
}
