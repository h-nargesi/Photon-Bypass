namespace PhotonBypass.Domain.Profile;

public interface ICloudRepository
{
    Task<int> FindWebCloud();
}
