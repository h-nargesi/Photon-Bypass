using PhotonBypass.FreeRadius.Entity;
using PhotonBypass.FreeRadius.Interfaces;

namespace PhotonBypass.FreeRadius.Repository;

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
