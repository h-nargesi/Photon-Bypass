namespace PhotonBypass.RadiusDesk.Interfaces;

public interface ICloudRepository
{
    Task<int> FindWebCloud();
}
