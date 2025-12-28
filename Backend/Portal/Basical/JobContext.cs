using PhotonBypass.Domain;

namespace PhotonBypass.Portal.Basical;

class JobContext : IJobContext
{
    public string Username { get; set; } = null!;

    public string Target { get; set; } = null!;

    public int? AccountId { get; set; }

    public void InjectJobContext(int account_id)
    {
        AccountId = account_id;
    }

    public void InjectJobContext(int account_id, string username, string target)
    {
        AccountId = account_id;
        Username = username;
        Target = target;
    }
}
