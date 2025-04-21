namespace PhotonBypass.Domain.Profile;

public interface IPermanentUsersRepository
{
    Task<PermanentUserEntity?> GetUser(string username);

    Task<bool> CheckUsername(string username);

    Task<string> GetRestrictedServer(string username);

    Task<UserPlanStateEntity?> GetPlanState(string username);
}
