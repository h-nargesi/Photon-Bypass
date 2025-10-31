namespace PhotonBypass.Domain.Profile;

public interface IStaticRepository
{
    int WebCloudId { get; }

    ProfileEntity DefaultProfile { get; }
}
