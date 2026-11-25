namespace PhotonBypass.Domain;

public interface IJobContext
{
    string Username { get; }

    string Target { get; }

    /// <summary>Panel user the job acts for — the billing payer (Docs/20 D4); set only by InjectJobContext.</summary>
    int? UserId { get; }

    void InjectJobContext(int user_id, string username, string target);
}
