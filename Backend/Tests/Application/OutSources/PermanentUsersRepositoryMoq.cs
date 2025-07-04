using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Application.OutSources;

class PermanentUsersRepositoryMoq : Mock<IPermanentUsersRepository>, IOutSourceMoq
{
    public Dictionary<string, PermanentUserEntity> Data { get; private set; } = null!;

    public event Action<string, PermanentUserEntity?>? OnGetUser;

    public event Action<int, PermanentUserEntity?>? OnGetUserById;

    public event Action<string, bool>? OnCheckUsername;

    public event Action<IEnumerable<int>, Dictionary<int, (string? Phone, string? Email)>>? OnGetUsersContactInfo;

    public void Setup()
    {
        var raw_text = File.ReadAllText("Data/permanent-users.json");
        Data = System.Text.Json.JsonSerializer.Deserialize<List<PermanentUserEntity>>(raw_text)
            ?.ToDictionary(x => x.Username)
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
    }

    public static PermanentUsersRepositoryMoq CreateInstance(IServiceCollection services)
    {
        var moq = new PermanentUsersRepositoryMoq();
        moq.Setup();
        services.AddLazyScoped(s => moq.Object);
        return moq;
    }
}
