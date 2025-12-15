namespace PhotonBypass.Domain.Account.Model;

public enum EventType : byte
{
    Information = 1,
    Success = 2,
    Warning = 3,
    Error = 4,
    Critical = 5,
}