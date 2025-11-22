using PhotonBypass.FreeRadius.Entity;

namespace PhotonBypass.FreeRadius.Interfaces;

public interface ITopUpRepository
{
    Task<TopUpEntity?> LatestOf(int user_id);
}
