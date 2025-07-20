using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

class PermanentUsersRepositoryMoq : Mock<IPermanentUsersRepository>, IOutSourceMoq
{
    public Dictionary<string, PermanentUserEntity> Data { get; private set; } = null!;

    public event Action<string, PermanentUserEntity?>? OnGetUser;

    public event Action<int, PermanentUserEntity?>? OnGetUserById;

    public event Action<string, bool>? OnCheckUsername;

    public event Action<IEnumerable<int>, Dictionary<int, (string? Phone, string? Email)>>? OnGetUsersContactInfo;

    public PermanentUsersRepositoryMoq Setup(IDataSource source)
    {
        var raw_text = File.ReadAllText(source.FilePath);
        Data = System.Text.Json.JsonSerializer.Deserialize<List<PermanentUserMoqModel>>(raw_text)
            ?.Select(x => x.ToEntity())
            .ToDictionary(x => x.Username)
            ?? [];

        Setup(x => x.GetUser(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                if (!Data.TryGetValue(username, out var entity))
                {
                    entity = null;
                }

                OnGetUser?.Invoke(username, entity);

                return Task.FromResult(entity);
            });

        Setup(x => x.GetUser(It.IsAny<int>()))
            .Returns<int>(id =>
            {
                var result = Data.Values.FirstOrDefault(x => x.Id == id);
                OnGetUserById?.Invoke(id, result);
                return Task.FromResult(result);
            });

        Setup(x => x.CheckUsername(It.IsNotNull<string>()))
            .Returns<string>(username =>
            {
                var result = Data.ContainsKey(username);
                OnCheckUsername?.Invoke(username, result);
                return Task.FromResult(result);
            });

        Setup(x => x.GetUsersContactInfo(It.IsNotNull<IEnumerable<int>>()))
            .Returns<IEnumerable<int>>(userids =>
            {
                var result = Data.Values.Where(x => userids.Contains(x.Id))
                    .ToDictionary(k => k.Id, v => (v.Phone, v.Email));
                OnGetUsersContactInfo?.Invoke(userids, result);
                return Task.FromResult(result as IDictionary<int, (string? Phone, string? Email)>);
            });

        return this;
    }

    IOutSourceMoq IOutSourceMoq.Setup(IDataSource source)
    {
        return Setup(source);
    }

    public class DataSource : IDataSource
    {
        public string FilePath { get; set; } = "Data/permanent-users.json";
    }

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped(s => new DataSource());
        services.AddScoped(s => new PermanentUsersRepositoryMoq().Setup(s.GetRequiredService<DataSource>()));
        services.AddLazyScoped(s => s.GetRequiredService<PermanentUsersRepositoryMoq>().Object);
    }
}
