using PhotonBypass.RadiusDesk.Entity;

namespace PhotonBypass.RadiusDesk.Interfaces;

public interface IStaticRepository
{
    Task<int> WebCloudId { get; }

    Task<ProfileEntity> DefaultProfile { get; }
}
