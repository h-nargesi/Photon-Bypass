using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Tools;
using tik4net;
using tik4net.Objects;

namespace PhotonBypass.Test.Mock.MockServerBridge;

internal class Tik4NetHandlerMoq : Mock<ITik4NetHandler>, IOutSourceMoq
{
    public event Action<string, List<ITikCommandParameter>>? OnExecute;

    public Tik4NetHandlerMoq()
    {
        Setup(x => x.ConnectTo(It.IsAny<ServerEntity>()))
            .Returns<ServerEntity>(server => Task.FromResult(CreateMoq(server)));
    }

    public IEnumerable<TModel> GetData<TModel>() where TModel : new()
    {
        return Object.ConnectTo(null!).Result.LoadList<TModel>();
    }

    public void Execute(string command_text, List<ITikCommandParameter> parameters)
    {
        OnExecute?.Invoke(command_text, parameters);
    }

    private ITikConnection CreateMoq(ServerEntity server)
    {
        var connection_mock = new Mock<ITikConnection>();

        connection_mock.Setup(connection => connection.CreateCommand(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<ITikCommandParameter[]>()))
            .Returns<string, TikCommandParameterFormat, ITikCommandParameter[]>((command_text, _, parameters) =>
                new TikCommandMoq(this, server, command_text, parameters).Object);

        connection_mock.Setup(connection => connection.CreateCommandAndParameters(It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>(), It.IsAny<string[]>()))
            .Returns<string, TikCommandParameterFormat, string[]>((command_text, format, parameters) =>
                new TikCommandMoq(this, server, command_text, format, parameters).Object);

        connection_mock.Setup(connection => connection.CreateParameter(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((name, value) => new TikParam(name, value));

        return connection_mock.Object;
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<Tik4NetHandlerMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<Tik4NetHandlerMoq>().Object);
    }
}
