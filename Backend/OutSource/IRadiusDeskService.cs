using PhotonBypass.Domain.Radius;

namespace PhotonBypass.OutSource;

public interface IRadiusDeskService : IDisposable
{
    Task SavePermenentUser(PermenantUserEntity user);
}
