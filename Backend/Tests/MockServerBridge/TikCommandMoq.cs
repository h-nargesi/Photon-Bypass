using Moq;
using System.Text.Json;
using tik4net;

namespace PhotonBypass.Test.MockServerBridge;

internal class TikCommandMoq : Mock<ITikCommand>, IOutSourceMoq
{
    private readonly List<ITikCommandParameter> parameters = [];

    public TikCommandMoq(string command_text, IEnumerable<ITikCommandParameter> parameters)
    {
        this.parameters.AddRange(parameters);

        Setup(command => command.Parameters).Returns(() => this.parameters);

        Setup(command => command.ExecuteList())
            .Returns(() => GetSessions($"Data/mikrotik{command_text.Replace('/', '-')}.json"));
    }

    private IEnumerable<ITikReSentence> GetSessions(string file_name)
    {
        var raw_text = File.ReadAllText(file_name);
        IEnumerable<TikReSentence>? session_list = JsonSerializer.Deserialize<List<TikReSentence>>(raw_text);

        if (session_list == null) return [];

        foreach (var param in parameters)
            session_list = session_list.Where(l => l.TryGetValue(param.Name, out var value) && value == param.Value);

        return session_list ?? [];
    }

    public class TikReSentence : Dictionary<string, string>, ITikReSentence
    {
        public IReadOnlyDictionary<string, string> Words => this;

        public string Tag => throw new NotImplementedException();

        public string GetId() => this[".id"];

        public string GetResponseField(string fieldName) => this[fieldName];

        public string GetResponseFieldOrDefault(string fieldName, string defaultValue)
        {
            if (TryGetValue(fieldName, out var fieldValue))
            {
                return fieldValue;
            }

            return defaultValue;
        }

        public bool TryGetResponseField(string fieldName, out string? fieldValue) => TryGetValue(fieldName, out fieldValue);
    }
}
