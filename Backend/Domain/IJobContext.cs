namespace PhotonBypass.Domain;

public interface IJobContext
{
    string Username { get; }

    string Target { get; }
}
