using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Radius.Repository;

class StaticRepository : IStaticRepository
{
    public StaticRepository(ICloudRepository cloud_repo,
        IProfileRepository profile_repo)
    {
        WebCloudID = cloud_repo.FindWebCloud().Result;
        DefaultProfile = profile_repo.FindDefaultProfile(WebCloudID).Result;
    }

    public int WebCloudID { get; }

    public ProfileEntity DefaultProfile { get; }
}
