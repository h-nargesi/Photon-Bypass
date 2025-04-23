namespace PhotonBypass.Domain.Profile;

public interface ITopUpRepository
{
    Task<TopUpEntity?> LatestOf(int user_id);
}
