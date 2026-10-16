using PhotonBypass.RadiusDesk.Entity;

namespace PhotonBypass.RadiusDesk.Interfaces;

public interface ITopUpRepository
{
    Task<TopUpEntity?> LatestOf(int user_id);
}
