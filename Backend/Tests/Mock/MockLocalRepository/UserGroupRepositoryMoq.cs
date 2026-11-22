using System.Text.Json;
using Moq;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.MockOptions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Mock.MockLocalRepository;

/// <summary>In-memory <see cref="IUserGroupRepository"/> over <c>Data/Local/user-group.json</c> (Docs/24 D6 fixtures).</summary>
public class UserGroupRepositoryMoq : Mock<IUserGroupRepository>, IUnitLevelService
{
    public readonly List<UserGroupEntity> Data;

    public UserGroupRepositoryMoq() : this(FilePath)
    {
    }

    protected UserGroupRepositoryMoq(string file_path)
    {
        Data = JsonSerializer.Deserialize<List<UserGroupEntity>>(File.ReadAllText(file_path)) ?? [];

        Setup(repository => repository.GetAll())
            .Returns(() => Task.FromResult(Data.ToList()));

        Setup(repository => repository.Find(It.IsAny<int>()))
            .Returns<int>(id => Task.FromResult(Data.FirstOrDefault(group => group.Id == id)));

        Setup(repository => repository.FindByTitle(It.IsAny<string>()))
            .Returns<string>(title => Task.FromResult(Data.FirstOrDefault(group => group.Title == title)));

        Setup(repository => repository.GetDefault())
            .Returns(() => Task.FromResult(Data.First(group => group.IsDefault)));

        Setup(repository => repository.HasMembers(It.IsAny<int>()))
            .Returns<int>(_ => Task.FromResult(false));

        Setup(repository => repository.GetMemberCounts())
            .Returns(() => Task.FromResult(new Dictionary<int, int>()));

        Setup(repository => repository.ClearDefault(It.IsAny<int>()))
            .Callback<int>(except_id =>
            {
                foreach (var group in Data.Where(group => group.IsDefault && group.Id != except_id))
                {
                    group.IsDefault = false;
                }
            })
            .Returns(Task.CompletedTask);

        Setup(repository => repository.Save(It.IsAny<UserGroupEntity>()))
            .Returns<UserGroupEntity>(group =>
            {
                var existed = Data.FirstOrDefault(g => g.Id == group.Id);

                if (existed == null)
                {
                    group.Id = Data.Count > 0 ? Data.Max(g => g.Id) + 1 : 1;
                    Data.Add(group);
                }
                else
                {
                    Data.Remove(existed);
                    Data.Add(group);
                }

                return Task.CompletedTask;
            });

        Setup(repository => repository.Delete(It.IsAny<UserGroupEntity>()))
            .Returns<UserGroupEntity>(group =>
            {
                Data.RemoveAll(g => g.Id == group.Id);
                return Task.CompletedTask;
            });
    }

    private const string FilePath = "Data/Local/user-group.json";

    public static void CreateInstance(IServiceCollection services)
    {
        services.AddScoped<UserGroupRepositoryMoq>();
        services.AddLazyTransient(provider => provider.GetRequiredService<UserGroupRepositoryMoq>().Object);
    }
}
