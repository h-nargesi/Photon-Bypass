using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockServerBridge;

class SshHandlerMoq : Mock<ISshHandler>, IUnitLevelService
{
    public SshHandlerMoq()
    {
        var connection = new Mock<ISshConnection>();

        connection.Setup(cl => cl.Execute(It.IsAny<string>(), out It.Ref<string>.IsAny))
            .Returns((string command, out string result) =>
            {
                result = Execute(command);
                return true;
            });

        Setup(handler => handler.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns(Task.FromResult(connection.Object));
    }

    private static string Execute(string command)
    {
        if (command.StartsWith("/certificate print") ||
            command.StartsWith("/file print") ||
            command.StartsWith("/file remove") ||
            command.StartsWith("/certificate export-certificate"))
            return "some data";

        else if (command.StartsWith(":put [/file get"))
            return KeyCode;

        throw new NotImplementedException();
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<SshHandlerMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<SshHandlerMoq>().Object);
    }

    private const string KeyCode = @"
GLn9PvrGidPUKqEKbJxGVyjMAfDeORQ1v4hYgWGR8Weu6YRSenRXVjKBwgd7TmB7
yUQCTEcSCMkAt6JS6/Lg49KwX+pVU8P/Y5o8oXHiY1UQpI/2IcNJCqauHEK4XNLI
LtglVCraHTkFHIR/0K9EpKA+fYzgg0Gfuyn4Od7/Q1QTG/6c37n/dtK9lHY+K193
MIIFqjCCA5KgAwIBAgIIaS85PwYa0Q0wDQYJKoZIhvcNAQELBQAwWjELMAkGA1UE
BhMCSVIxCzAJBgNVBAgMAlRIMQ8wDQYDVQQHDAZUZWhyYW4xDzANBgNVBAoMBlBo
b3RvbjEMMAoGA1UECwwDVlBOMQ4wDAYDVQQDDAVMTVRDQTAeFw0yMzAzMTgyMDU0
MIIFqjCCA5KgAwIBAgIIaS85PwYa0Q0wDQYJKoZIhvcNAQELBQAwWjELMAkGA1UE
BhMCSVIxCzAJBgNVBAgMAlRIMQ8wDQYDVQQHDAZUZWhyYW4xDzANBgNVBAoMBlBo
b3RvbjEMMAoGA1UECwwDVlBOMQ4wDAYDVQQDDAVMTVRDQTAeFw0yMzAzMTgyMDU0
x/kDzKbGAiAagchH0NCNmkh2XuYsHsIYYVS5SAEQOzj6jYZLjnGrkOu0zPA5G6CH
GLn9PvrGidPUKqEKbJxGVyjMAfDeORQ1v4hYgWGR8Weu6YRSenRXVjKBwgd7TmB7
x/kDzKbGAiAagchH0NCNmkh2XuYsHsIYYVS5SAEQOzj6jYZLjnGrkOu0zPA5G6CH
yUQCTEcSCMkAt6JS6/Lg49KwX+pVU8P/Y5o8oXHiY1UQpI/2IcNJCqauHEK4XNLI
LtglVCraHTkFHIR/0K9EpKA+fYzgg0Gfuyn4Od7/Q1QTG/6c37n/dtK9lHY+K193
";
}
