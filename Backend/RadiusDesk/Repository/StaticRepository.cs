using PhotonBypass.RadiusDesk.Entity;
using PhotonBypass.RadiusDesk.Interfaces;

namespace PhotonBypass.RadiusDesk.Repository;

class StaticRepository(
    Lazy<ICloudRepository> CloudRepo,
    Lazy<IProfileRepository> ProfileRepo) : IStaticRepository
{
    private Task<int>? web_cloud_id;
    private Task<ProfileEntity>? default_profile;

    public Task<int> WebCloudId => web_cloud_id ??= CloudRepo.Value.FindWebCloud();

    public Task<ProfileEntity> DefaultProfile => default_profile ??= LoadDefaultProfile();

    private async Task<ProfileEntity> LoadDefaultProfile()
    {
        var cloud_id = await WebCloudId;

        return await ProfileRepo.Value.FindDefaultProfile(cloud_id);
    }
}
