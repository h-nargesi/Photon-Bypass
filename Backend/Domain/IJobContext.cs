namespace PhotonBypass.Domain;

public interface IJobContext
{
    string Username { get; }

    string Target { get; }

    /// <summary>The VPN account the current request acts on (Docs/22 §3); null on user-level calls.</summary>
    int? AccountId { get; }

    void InjectJobContext(int account_id);

    void InjectJobContext(int account_id, string username, string target);
}
