using PhotonBypass.Domain;

namespace PhotonBypass.API.Basical;

public class JobContext : IJobContext
{
    public string Username { get; set; } = null!;

    public string Target { get; set; } = null!;
}
