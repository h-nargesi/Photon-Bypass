using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.LocalDatabase;

public class TransactionTest : OutSourceLevelServiceInitializer
{
    [Fact]
    public async Task ShouldCreateNewTransaction()
    {
        using var scope = App.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OutSourceManager>()
            .InitializeOutSource<LocalDatabaseInitializer>("DbTest1");        
    }
}