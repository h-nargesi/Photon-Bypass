namespace PhotonBypass.Infra;

public class UserException(string? message = null, string? developer = null) : Exception(message)
{
    public string? Developer { get; } = developer;
}
