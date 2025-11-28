using PhotonBypass.Domain;

namespace PhotonBypass.Portal.Basical;

class JobContext : IJobContext
{
    public string Username { get; set; } = null!;

    public string Target { get; set; } = null!;
}
