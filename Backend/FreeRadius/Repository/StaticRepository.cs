using PhotonBypass.Domain.Plan;
using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;

namespace PhotonBypass.FreeRadius.Repository;

class StaticRepository(
    Lazy<ICloudRepository> CloudRepo,
    Lazy<IProfileRepository> ProfileRepo) : IStaticRepository
{
    private int? webCloudId;
    private ProfileEntity? defaultProfile;

    public int WebCloudId => webCloudId ??= CloudRepo.Value.FindWebCloud().Result;

    public ProfileEntity DefaultProfile => defaultProfile ??= ProfileRepo.Value.FindDefaultProfile(WebCloudId).Result;
}