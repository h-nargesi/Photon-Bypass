namespace PhotonBypass.Domain.Profile;

public interface IPermanentUsersRepository
{
    Task<PermanentUserEntity?> GetUser(int id);

    Task<PermanentUserEntity?> GetUser(string username);

    Task<bool> CheckUsername(string username);

    Task<string?> GetRestrictedServer(int id);

    Task<UserPlanStateEntity?> GetPlanState(int id);

    Task<UserPlanStateEntity?> GetPlanState(string username);
}
