using PhotonBypass.Domain.Profile;

namespace PhotonBypass.Domain.Radius;

public interface IRadiusDeskService : IDisposable
{
    Task SavePermenentUser(PermenantUserEntity user);
}
