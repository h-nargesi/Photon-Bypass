using PhotonBypass.Domain.Profile;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;

namespace PhotonBypass.FreeRadius.Repository;

class StaticRepository : IStaticRepository
{
    public StaticRepository(ICloudRepository cloud_repo,
        IProfileRepository profile_repo)
    {
        WebCloudId = cloud_repo.FindWebCloud().Result;
        DefaultProfile = profile_repo.FindDefaultProfile(WebCloudId).Result;
    }

    public int WebCloudId { get; }

    public ProfileEntity DefaultProfile { get; }
}
