using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Interfaces;

public interface IStaticRepository
{
    Task<int> WebCloudId { get; }

    Task<ProfileEntity> DefaultProfile { get; }
}
