using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using tik4net;

namespace PhotonBypass.Test.MockServerBridge;

internal class Tik4NetHandlerMoq : Mock<ITik4NetHandler>, IOutSourceMoq
{
    public event Action<string, List<ITikCommandParameter>>? OnDelete;

    public Tik4NetHandlerMoq()
    {
        var connection = new Mock<ITikConnection>();

        Setup(x => x.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(server => Task.FromResult(connection.Object));

        connection.Setup(connection => connection.CreateCommand(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<ITikCommandParameter[]>()))
            .Returns<string, TikCommandParameterFormat, ITikCommandParameter[]>((commandText, _, parameters) =>
            new TikCommandMoq(this, commandText, parameters).Object);

        connection.Setup(connection => connection.CreateCommandAndParameters(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<string[]>()))
            .Returns<string, TikCommandParameterFormat, string[]>((commandText, _, parameters) =>
            new TikCommandMoq(this, commandText, parameters).Object);
    }

    public void Delete(string command_text, List<ITikCommandParameter> parameters)
    {
        OnDelete?.Invoke(command_text, parameters);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<Tik4NetHandlerMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<Tik4NetHandlerMoq>().Object);
    }
}
