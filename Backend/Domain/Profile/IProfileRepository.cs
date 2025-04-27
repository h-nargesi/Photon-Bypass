namespace PhotonBypass.Domain.Profile;

public interface IProfileRepository
{
    Task<ProfileEntity> FindDefaultProfile(int cloud_id);

    Task<ProfileEntity> GetProfile(int cloud_id, PlanType type, int count);
}
