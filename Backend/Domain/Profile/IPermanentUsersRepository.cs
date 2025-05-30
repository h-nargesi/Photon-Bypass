namespace PhotonBypass.Domain.Profile;

public interface IPermanentUsersRepository
{
    Task<PermanentUserEntity?> GetUser(int id);

    Task<PermanentUserEntity?> GetUser(string username);

    Task<bool> CheckUsername(string username);

    Task<IDictionary<int, (string? Phone, string? Email)>> GetUsersContactInfo(IEnumerable<int> userids);
}
