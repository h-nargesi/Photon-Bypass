using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Interfaces;

public interface IStaticRepository
{
    int WebCloudId { get; }

    ProfileEntity DefaultProfile { get; }
}
