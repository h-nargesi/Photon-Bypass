using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class DatabaseGlobalTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "DbTest1";

    [Fact]
    public async Task ShouldCreateNewTransaction()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var user = new PanelUserEntity
        {
            Name = "Transaction",
            Surname = "Transaction",
            Password = PasswordHasher.Hash("password"),
            Username = "Transaction",
        };

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();

        await user_repo.DbContext.BeginTransactionAsync();
        await user_repo.Save(user);
        await user_repo.DbContext.RollbackAsync();

        var saved = await user_repo.GetUser("U0059");

        Assert.Null(saved);
    }

    [Fact]
    public async Task SetNewIdOnInsert()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var user = new PanelUserEntity
        {
            Name = "SetNewId",
            Surname = "SetNewId",
            Password = PasswordHasher.Hash("password"),
            Username = "SetNewId",
        };

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();

        await user_repo.Save(user);

        Assert.True(user.Id > 0);

        var saved = await user_repo.GetActiveUser(user.Id);

        Assert.NotNull(saved);
        Assert.Equal(user.Username, saved.Username);
    }

    [Fact]
    public async Task SetNewIdOnSaveRange()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var users = new List<PanelUserEntity>
        {
            new()
            {
                Name = "SetNewId",
                Surname = "SetNewIdBulk1",
                Password = PasswordHasher.Hash("password"),
                Username = "SetNewIdBulk1",
            },
            new()
            {
                Name = "SetNewId",
                Surname = "SetNewId",
                Password = PasswordHasher.Hash("password"),
                Username = "SetNewIdBulk2",
            }
        };

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();

        await user_repo.Save(users);

        foreach (var user in users)
        {
            Assert.True(user.Id > 0);
        }

        var saveds = await user_repo.GetActiveUsers(users.Select(a => a.Id));

        foreach (var user in users)
        {
            Assert.True(saveds.TryGetValue(user.Id, out var saved));
            Assert.Equal(user.Username, saved.Username);
        }
    }
}
