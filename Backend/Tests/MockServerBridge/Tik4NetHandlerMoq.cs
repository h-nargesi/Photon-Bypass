using Moq;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.ServerBridge.Services;
using PhotonBypass.Tools;
using System.Linq;
using System.Text.Json;
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
            .Returns<string, TikCommandParameterFormat, ITikCommandParameter[]>((commandText, defaultParameterFormat, parameters) =>
            {
                var command = new Mock<ITikCommand>();

                switch (commandText)
                {
                    case "/user-manager/session/print":
                        command.Setup(command => command.ExecuteList())
                            .Returns(() => GetSessions(parameters));
                        break;
                }

                return command.Object;
            });
    }

    private IEnumerable<ITikReSentence> GetSessions(ITikCommandParameter[] parameters)
    {
        var raw_text = File.ReadAllText("Data/mikrotik-sessions.json");
        IEnumerable<SessionModel>? session_list = JsonSerializer.Deserialize<List<SessionModel>>(raw_text);

        if (session_list == null) return [];

        foreach (var param in parameters)
            switch (param.Name)
            {
                case "username":
                    session_list.Where(x => x.Username == param.Value);
                    break;
            }

        return session_list.Select();
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddSingleton<Tik4NetHandlerMoq>();
        services.AddLazySingleton(s => s.GetRequiredService<Tik4NetHandlerMoq>().Object);
    }
}
