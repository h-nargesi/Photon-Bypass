namespace PhotonBypass.ErrorHandler;

public class UserException : Exception
{
    public UserException(string? message = null, string? detail = null) : base(detail ?? message)
    {
        UserMessage = message;
    }

    public UserException(string? message, Exception inner_exception) : base(message, inner_exception)
    {
        UserMessage = message;
    }

    public string? UserMessage { get; }
}