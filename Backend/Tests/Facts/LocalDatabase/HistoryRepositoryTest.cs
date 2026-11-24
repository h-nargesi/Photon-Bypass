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
        var user_id = await scope.ServiceProvider.GetRequiredService<IPanelUserRepository>()
            .GetActiveUserId("User99");
        Assert.NotNull(user_id);

        var now = new DateTime(10000 * (DateTime.Now.Ticks / 10000));

        var history = new HistoryEntity()
        {
            Category = EventCategory.Renewal,
            Description = "Description",
            Title = "Title",
            Type = EventType.Success,
            Value = "Value",
            Price = 1200,
            Target = user_id!.Value,
            Created = now.AddDays(-10),
        };

        await history_repo.Save(history);
        var histories = await history_repo.GetHistory(user_id!.Value, now.AddDays(-11), null);

        Assert.NotNull(histories);
        AssertHistories([history], histories.ToDictionary(h => h.Title));
    }

    [Fact]
    public async Task BatchInsert_FetchSimple()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();
        var user_id = await scope.ServiceProvider.GetRequiredService<IPanelUserRepository>()
            .GetActiveUserId("User99");
        Assert.NotNull(user_id);

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
                Target = user_id!.Value,
                Created = now.AddDays(-10),
            },
            new() {
                Category = EventCategory.Security,
                Description = "Description2",
                Title = "Title2",
                Type = EventType.Success,
                Value = "Value2",
                Price = 1100,
                Target = user_id!.Value,
                Created = now.AddDays(-9),
            }
        };

        await history_repo.Save(saving_history);
        var histories = await history_repo.GetHistory(user_id!.Value, now.AddDays(-11), null);

        Assert.NotNull(histories);
        AssertHistories(saving_history, histories.ToDictionary(h => h.Title));

        saving_history = [.. histories];
        foreach (var history in histories)
            history.Issuer  = 1;

        await history_repo.Save(saving_history);
        histories = await history_repo.GetHistory(user_id!.Value, now.AddDays(-11), null);

        Assert.NotNull(histories);
        AssertHistories(saving_history, histories.ToDictionary(h => h.Title));
    }

    [Fact]
    public async Task GetHistory_DateBounds_KeepTheTargetFilter()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage1, this);

        var user_repo = scope.ServiceProvider.GetRequiredService<IPanelUserRepository>();
        var history_repo = scope.ServiceProvider.GetRequiredService<IHistoryRepository>();

        var user_id = await user_repo.GetActiveUserId("User99");
        var other_id = await user_repo.GetActiveUserId("User2");
        Assert.NotNull(user_id);
        Assert.NotNull(other_id);

        var now = new DateTime(10000 * (DateTime.Now.Ticks / 10000));

        HistoryEntity[] rows =
        [
            NewHistory(user_id!.Value, "Mine", now.AddDays(-10)),
            NewHistory(other_id!.Value, "Theirs", now.AddDays(-9)),
        ];

        await history_repo.Save(rows);

        async Task<string[]> Titles(DateTime? from, DateTime? to) =>
            (await history_repo.GetHistory(user_id.Value, from, to))
            .Select(history => history.Title)
            .ToArray();

        async Task AssertScopedWindow(DateTime? from, DateTime? to)
        {
            var titles = await Titles(from, to);
            Assert.Contains("Mine", titles);
            Assert.DoesNotContain("Theirs", titles);
        }

        await AssertScopedWindow(null, null);
        await AssertScopedWindow(now.AddDays(-11), null);
        await AssertScopedWindow(null, now.AddDays(-5));
        await AssertScopedWindow(now.AddDays(-11), now.AddDays(-5));

        Assert.Empty(await Titles(now.AddDays(1), null));
    }

    private static HistoryEntity NewHistory(int target, string title, DateTime created) => new()
    {
        Category = EventCategory.Renewal,
        Description = "Description",
        Title = title,
        Type = EventType.Success,
        Value = "Value",
        Price = 1200,
        Target = target,
        Created = created,
    };

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