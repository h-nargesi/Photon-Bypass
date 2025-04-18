namespace PhotonBypass.Domain.Profile;

public interface IProfileRepository
{
    Task<ProfileEntity> FindDefaultProfile(int cloud_id);
}
