using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using tik4net;

namespace PhotonBypass.Test.MockServerBridge;

internal class Tik4NetHandlerMoq : Mock<ITik4NetHandler>, IOutSourceMoq
{
    public event Action<string, List<ITikCommandParameter>>? OnExecute;

    public Tik4NetHandlerMoq()
    {
        var connection_mock = new Mock<ITikConnection>();

        Setup(x => x.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(server => Task.FromResult(connection_mock.Object));

        connection_mock.Setup(connection => connection.CreateCommand(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<ITikCommandParameter[]>()))
            .Returns<string, TikCommandParameterFormat, ITikCommandParameter[]>((command_text, _, parameters) =>
            new TikCommandMoq(this, command_text, parameters).Object);

        connection_mock.Setup(connection => connection.CreateCommandAndParameters(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<string[]>()))
            .Returns<string, TikCommandParameterFormat, string[]>((command_text, _, parameters) =>
            new TikCommandMoq(this, command_text, parameters).Object);
    }

    public void Execute(string command_text, List<ITikCommandParameter> parameters)
    {
        OnExecute?.Invoke(command_text, parameters);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<Tik4NetHandlerMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<Tik4NetHandlerMoq>().Object);
    }
}
