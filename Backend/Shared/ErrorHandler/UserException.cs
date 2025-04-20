namespace PhotonBypass.ErrorHandler;

public class UserException(string? message = null, string? detail = null) : Exception(detail ?? message)
{
    public string? UserMessage { get; } = message;
}
