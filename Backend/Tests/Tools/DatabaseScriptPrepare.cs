namespace PhotonBypass.Test.Tools;

public static class DatabaseScriptPrepare
{
    public static List<string> ReplaceDatabaseName(string name, List<string> contents)
    {
        return contents.Select(content => content.Replace("FastBypass", $"FastBypass_{name}"))
            .ToList();
    }
}