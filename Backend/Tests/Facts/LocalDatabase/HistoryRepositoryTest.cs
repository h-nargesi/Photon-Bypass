using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

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
        Assert.Single(histories);
        Assert.Equal(history.Category, histories[0].Category);
        Assert.Equal(history.Description, histories[0].Description);
        Assert.Equal(history.Title, histories[0].Title);
        Assert.Equal(history.Type, histories[0].Type);
        Assert.Equal(history.Value, histories[0].Value);
        Assert.Equal(history.Price, histories[0].Price);
        Assert.Equal(history.Target, histories[0].Target);
        Assert.Equal(history.Created, histories[0].Created);
    }
}