using PhotonBypass.Domain.Radius;
using PhotonBypass.Infra.Database.Radius;

namespace PhotonBypass.Application.Database;

class StaticRepository
{
    public StaticRepository(IRadRepository<CloudsEntity> cloud_repo,
        IRadRepository<ProfileEntity> profile_repo)
    {
        var cloudTask = FindWebCloud(cloud_repo);
        var profileTask = FindDefaultProfile(profile_repo);

        WebCloudID = cloudTask.Result;
        DefaultProfile = profileTask.Result;
    }

    public int WebCloudID { get; }

    public ProfileEntity DefaultProfile { get; }

    private static async Task<int> FindWebCloud(IRadRepository<CloudsEntity> repository)
    {
        var result = await repository.FindAsync(statement => statement
            .Where($"{nameof(CloudsEntity.Name)} = 'Web'"));

        var cloud = result.FirstOrDefault();

        return cloud?.Id ?? -1;
    }

    private static Task<ProfileEntity> FindDefaultProfile(IRadRepository<ProfileEntity> repository)
    {
        throw new NotImplementedException();
    }
}
