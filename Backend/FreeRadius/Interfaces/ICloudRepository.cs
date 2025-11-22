namespace PhotonBypass.FreeRadius.Interfaces;

public interface ICloudRepository
{
    Task<int> FindWebCloud();
}
