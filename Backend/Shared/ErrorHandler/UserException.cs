namespace PhotonBypass.ErrorHandler;

public class UserException : Exception
{
    public UserException(string? message = null, string? detail = null, short? code = null) : base(detail ?? message)
    {
        UserMessage = message;
        HttpCode = code;
    }

    public UserException(string? message, Exception inner_exception) : base(message, inner_exception)
    {
        UserMessage = message;
    }

    public string? UserMessage { get; }

    public short? HttpCode { get; }
}