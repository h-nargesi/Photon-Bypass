using Dapper;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Infra.Database;
using PhotonBypass.Radius.Repository.DbContext;
using PhotonBypass.Tools;

namespace PhotonBypass.Radius.Repository;

class PermanentUsersRepository(RadDbContext context) : DapperRepository<PermanentUserEntity>(context), IPermanentUsersRepository
{
    readonly static string TableName = EntityExtensions.GetTablename<PermanentUserEntity>();
    readonly static string Id = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Id);
    readonly static string Username = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Username);
    readonly static string Phone = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Phone);
    readonly static string Email = EntityExtensions.GetColumnName<PermanentUserEntity>(x => x.Email);

    public async Task<PermanentUserEntity?> GetUser(int id)
    {
        var result = await FindAsync(statement => statement
            .Where($"{Id} = @id")
            .WithParameters(new { id }));

        return result.FirstOrDefault();
    }

    public async Task<PermanentUserEntity?> GetUser(string username)
    {
        var result = await FindUserAsync(username);

        return result.FirstOrDefault();
    }

    public async Task<bool> CheckUsername(string username)
    {
        var result = await FindUserAsync(username);

        return result.Any();
    }

    public async Task<IDictionary<int, (string?, string?)>> GetUsersContactInfo(IEnumerable<int> userids)
    {
        var result = new Dictionary<int, (string?, string?)>();

        if (userids?.Any() != true) return result;

        var quety = $"select {Id} as Id, {Phone} as Phone, {Email} as Email from {TableName} where {Id} in (@userids)";

        var data = await connection.QueryAsync(quety, new { userids });

        foreach (var user in data)
        {
            result[user.Id] = (user.Phone, user.Email);
        }

        return result;
    }

    private Task<IEnumerable<PermanentUserEntity>> FindUserAsync(string username)
    {
        return FindAsync(statement => statement
            .Where($"{Username} = @username")
            .WithParameters(new { username }));
    }
}
