namespace PhotonBypass.Domain.Profile;

public interface IPermenantUsersRepository
{
    Task<PermenantUserEntity?> GetUser(string username);

    Task<bool> CheckUsername(string username);
}
