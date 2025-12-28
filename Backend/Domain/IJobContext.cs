namespace PhotonBypass.Domain;

public interface IJobContext
{
    string Username { get; }

    string Target { get; }

    int? AccountId { get; }

    void InjectJobContext(int account_id);

    void InjectJobContext(int account_id, string username, string target);
}
