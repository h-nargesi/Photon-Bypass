using PhotonBypass.Domain;

namespace PhotonBypass.Portal.Basical;

class JobContext : IJobContext
{
    public string Username { get; set; } = null!;

    public string Target { get; set; } = null!;

    public int? UserId { get; set; }

    public void InjectJobContext(int user_id, string username, string target)
    {
        UserId = user_id;
        Username = username;
        Target = target;
    }
}
