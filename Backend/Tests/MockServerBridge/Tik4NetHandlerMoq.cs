using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using tik4net;

namespace PhotonBypass.Test.MockServerBridge;

internal class Tik4NetHandlerMoq : Mock<ITik4NetHandler>, IOutSourceMoq
{
    public Tik4NetHandlerMoq()
    {
        var connection = new Mock<ITikConnection>();

        Setup(x => x.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(server => Task.FromResult(connection.Object));

        connection.Setup(connection => connection.CreateCommand(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<ITikCommandParameter[]>()))
            .Returns<string, TikCommandParameterFormat, ITikCommandParameter[]>((commandText, _, parameters) =>
            new TikCommandMoq(commandText, parameters).Object);
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<Tik4NetHandlerMoq>();
        services.AddLazySingleton(s => s.GetRequiredService<Tik4NetHandlerMoq>().Object);
    }
}
