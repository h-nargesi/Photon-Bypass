using PhotonBypass.Domain.Profile;
using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

class StaticRepository
{
    public StaticRepository(IRadRepository<CloudEntity> cloud_repo,
        IRadRepository<ProfileEntity> profile_repo)
    {
        WebCloudID = FindWebCloud(cloud_repo).Result;
        DefaultProfile = FindDefaultProfile(profile_repo, WebCloudID).Result;
    }

    public int WebCloudID { get; }

    public ProfileEntity DefaultProfile { get; }

    private static async Task<int> FindWebCloud(IRadRepository<CloudEntity> repository)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(CloudEntity.Name)} = 'Web'"));

        var cloud = result.FirstOrDefault();

        return cloud?.Id ?? -1;
    }

    private static async Task<ProfileEntity> FindDefaultProfile(IRadRepository<ProfileEntity> repository, int cloud_id)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($@"{nameof(ProfileEntity.SimultaneousUse)} = 1
                  and {nameof(ProfileEntity.MikrotikRateLimit)} is not null
                  and {nameof(ProfileEntity.CloudId)} = @cloud_id")
            .WithParameters(new { cloud_id })
            .OrderBy($"{nameof(ProfileEntity.MikrotikRateLimit)}"));

        return result.First();
    }
}
