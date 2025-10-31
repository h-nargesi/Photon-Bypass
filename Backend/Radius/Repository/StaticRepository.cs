using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Radius.Repository;

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
