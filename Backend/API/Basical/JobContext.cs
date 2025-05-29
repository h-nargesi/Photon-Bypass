using PhotonBypass.Domain;

namespace PhotonBypass.API.Basical;

class JobContext : IJobContext
{
    public string Username { get; set; } = null!;

    public string Target { get; set; } = null!;
}
