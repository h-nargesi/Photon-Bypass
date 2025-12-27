using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class HistoryRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage1 = "DbTest1";

    [Fact]
    public async Task Insert_FetchSimple()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        var accountId = await scope.ServiceProvider.GetRequiredService<IAccountRepository>()
            .GetAccountIdByUsername(["User1"]);

        var now = new DateTime(10000 * (DateTime.Now.Ticks / 10000));

        var history = new HistoryEntity()
        {
            Category = EventCategory.Renewal,
            Description = "Description",
            Title = "Title",
            Type = EventType.Success,
            Value = "Value",
            Price = 1200,
            Target = accountId["User1"],
            Created = now.AddDays(-10),
        };

        await history_repo.Save(history);
        var histories = await history_repo.GetHistory("User1", now.AddDays(-11), null);

        Assert.NotNull(histories);
        AssertHistories([history], histories.ToDictionary(h => h.Title));
    }

    [Fact]
    public async Task BatchInsert_FetchSimple()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        var accountId = await scope.ServiceProvider.GetRequiredService<IAccountRepository>()
            .GetAccountIdByUsername(["User1"]);

        var now = new DateTime(10000 * (DateTime.Now.Ticks / 10000));

        var saving_history = new HistoryEntity[]
        {
            new() {
                Category = EventCategory.Renewal,
                Description = "Description",
                Title = "Title1",
                Type = EventType.Success,
                Value = "Value",
                Price = 1200,
                Target = accountId["User1"],
                Created = now.AddDays(-10),
            },
            new() {
                Category = EventCategory.Security,
                Description = "Description2",
                Title = "Title2",
                Type = EventType.Success,
                Value = "Value2",
                Price = 1100,
                Target = accountId["User1"],
                Created = now.AddDays(-9),
            }
        };

        await history_repo.Save(saving_history);
        var histories = await history_repo.GetHistory("User1", now.AddDays(-11), null);

        Assert.NotNull(histories);
        AssertHistories(saving_history, histories.ToDictionary(h => h.Title));

        foreach (var history in saving_history)
            history.Issuer  = 1;
    }

    private static void AssertHistories(HistoryEntity[] saved, Dictionary<string, HistoryEntity> loaded)
    {
        saved.Foreach(source =>
        {
            Assert.True(loaded.TryGetValue(source.Title, out var target));
            Assert.Equal(source.Category, target.Category);
            Assert.Equal(source.Description, target.Description);
            Assert.Equal(source.Title, target.Title);
            Assert.Equal(source.Type, target.Type);
            Assert.Equal(source.Value, target.Value);
            Assert.Equal(source.Price, target.Price);
            Assert.Equal(source.Target, target.Target);
            Assert.Equal(source.Created.Ticks / 1000000, target.Created.Ticks / 1000000);
        });
    }
}