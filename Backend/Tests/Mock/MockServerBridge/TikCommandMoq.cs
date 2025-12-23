using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.ServerBridge.Tik4net;
using PhotonBypass.Test.MockOptions;
using tik4net;

namespace PhotonBypass.Test.Mock.MockServerBridge;

internal class TikCommandMoq : Mock<ITikCommand>
{
    private readonly List<ITikCommandParameter> parameters = [];
    private readonly ServerEntity? server;

    public TikCommandMoq(Tik4NetHandlerMoq parent, ServerEntity? server, string command_text,
        IEnumerable<ITikCommandParameter> parameters)
    {
        this.parameters.AddRange(parameters);
        this.server = server;

        Setup(parent, command_text);
    }

    public TikCommandMoq(Tik4NetHandlerMoq parent, ServerEntity? server, string command_text,
        TikCommandParameterFormat format,
        IEnumerable<string> parameters)
    {
        var parameters_list = parameters.ToList();
        this.server = server;

        switch (format)
        {
            case TikCommandParameterFormat.NameValue:
                for (var i = 0; i < parameters_list.Count; i += 2)
                    this.parameters.Add(new TikParam(parameters_list[i], parameters_list[i + 1]));
                break;
            default:
                foreach (var parameter in parameters_list)
                    this.parameters.Add(new TikParam(parameter));
                break;
        }

        Setup(parent, command_text);
    }

    private void Setup(Tik4NetHandlerMoq parent, string command_text)
    {
        Setup(command => command.Parameters).Returns(() => parameters);

        Setup(command => command.AddParameter(It.IsAny<string>(), It.IsAny<string>()))
            .Returns<string, string>((name, value) =>
            {
                var result = new TikParam(name, value);
                parameters.Add(result);
                return result;
            });

        Setup(command =>
                command.AddParameter(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TikCommandParameterFormat>()))
            .Returns<string, string, TikCommandParameterFormat>((name, value, _) =>
            {
                var result = new TikParam(name, value);
                parameters.Add(result);
                return result;
            });

        Setup(command => command.ExecuteList())
            .Returns(() => LoadFile($"Data/Mikrotik/{command_text[1..].Replace('/', '-')}.json"));

        Setup(command => command.ExecuteNonQuery())
            .Callback(() => parent.Execute(command_text, parameters));

        Setup(command => command.ExecuteScalar())
            .Callback(() => parent.Execute(command_text, parameters));
    }

    private List<ITikReSentence> LoadFile(string file_name)
    {
        if (!File.Exists(file_name))
        {
            throw new FileNotFoundException(file_name);
        }

        var raw_text = File.ReadAllText(file_name)
            .PrepareAllDateTimes();
        IEnumerable<TikReSentence>? session_list = JsonSerializer.Deserialize<List<TikReSentence>>(raw_text);

        if (session_list == null || !session_list.Any()) return [];

        if (server != null)
        {
            session_list = session_list
                .Where(sentence => !sentence.TryGetValue(".server-id", out var value) ||
                                   int.TryParse(value, out var server_id) && server_id == server.Id);
        }

        foreach (var param in parameters)
        {
            if (param.Name.StartsWith('>'))
            {
                var name = param.Name[1..];
                session_list = session_list.Where(sentence => sentence.TryGetValue(name, out var value) &&
                                                              string.CompareOrdinal(value, param.Value) > 0);
            }
            else if (param.Name.StartsWith('<'))
            {
                var name = param.Name[1..];
                session_list = session_list.Where(sentence => sentence.TryGetValue(name, out var value) &&
                                                              string.CompareOrdinal(value, param.Value) > 0);
            }
            else
            {
                session_list = session_list.Where(sentence => sentence.TryGetValue(param.Name, out var value) &&
                                                              value == param.Value);
            }
        }

        return [.. session_list.Select(x => x as ITikReSentence)];
    }

    class TikReSentence : Dictionary<string, string>, ITikReSentence
    {
        public IReadOnlyDictionary<string, string> Words => this;

        public string Tag => throw new NotImplementedException();

        public string GetId() => this[".id"];

        public string GetResponseField(string field_name) => this[field_name];

        public string? GetResponseFieldOrDefault(string field_name, string default_value) =>
            TryGetValue(field_name, out var field_value) ? field_value : null;

        public bool TryGetResponseField(string field_name, out string? field_value) =>
            TryGetValue(field_name, out field_value);
    }
}