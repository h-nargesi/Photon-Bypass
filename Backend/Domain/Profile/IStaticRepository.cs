namespace PhotonBypass.Domain.Profile;

public interface IStaticRepository
{
    int WebCloudID { get; }

    ProfileEntity DefaultProfile { get; }
}
