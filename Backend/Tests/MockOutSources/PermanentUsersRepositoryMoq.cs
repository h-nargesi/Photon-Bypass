using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Profile;
using PhotonBypass.Test.MockOutSources.Models;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.MockOutSources;

internal class PermanentUsersRepositoryMoq : Mock<IPermanentUsersRepository>, IOutSourceMoq
{
    public Dictionary<string, PermanentUserEntity> Data { get; }

    public event Action<string, PermanentUserEntity?>? OnGetUser;

    public event Action<int, PermanentUserEntity?>? OnGetUserById;

    public event Action<string, bool>? OnCheckUsername;

    public event Action<IEnumerable<int>, Dictionary<int, (string? Phone, string? Email)>>? OnGetUsersContactInfo;

    public PermanentUsersRepositoryMoq() : this(FilePath)
    {
    }

    protected PermanentUsersRepositoryMoq(string file_path)
    {
        var raw_text = File.ReadAllText(file_path);
        Data = JsonSerializer.Deserialize<List<PermanentUserMoqModel>>(raw_text)
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
            .Returns<IEnumerable<int>>(user_ids =>
            {
                var result = Data.Values.Where(x => user_ids.Contains(x.Id))
                    .ToDictionary(k => k.Id, v => (v.Phone, v.Email));
                OnGetUsersContactInfo?.Invoke(user_ids, result);
                return Task.FromResult<IDictionary<int, (string? Phone, string? Email)>>(result);
            });
    }

    private const string FilePath = "Data/permanent-users.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<PermanentUsersRepositoryMoq>();
        services.AddLazyScoped(s => s.GetRequiredService<PermanentUsersRepositoryMoq>().Object);
    }
}