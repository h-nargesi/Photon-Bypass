using FluentAssertions;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.LocalDatabase;

/// <summary>
/// Realm rows on the real schema (Docs/24 D5): the nullable customer-facing
/// Title travels through the FastCrud insert and update and the id lookup
/// without loss, and the display fallback stays a computed property.
/// </summary>
public class RealmRepositoryTest : OutSourceLevelServiceInitializer
{
    private const string TestPackage = "RealmRepository";

    [Fact]
    public async Task PersianTitle_RoundTripsThroughSaveAndGetByIds()
    {
        using var scope = App.Services.CreateScope();
        await scope.Register<LocalDatabaseInitializer>(TestPackage, this);

        var realm_repo = scope.ServiceProvider.GetRequiredService<IRealmRepository>();

        var titled = new RealmEntity { IsActive = true, Name = "Titled-01", Title = "  سرور یکم  " };
        var untitled = new RealmEntity { IsActive = true, Name = "Untitled-1", Title = "   " };

        await realm_repo.Save([titled, untitled]);

        titled.Id.Should().BeGreaterThan(0);
        untitled.Id.Should().BeGreaterThan(0);

        // duplicate ids collapse; the lookup never sees them twice
        var loaded = await realm_repo.LoadRealms([titled.Id, untitled.Id, titled.Id]);

        // the column keeps the text as typed; trim and fallback live in DisplayTitle
        loaded.Should().HaveCount(2);
        loaded[titled.Id].Title.Should().Be("  سرور یکم  ");
        loaded[titled.Id].DisplayTitle.Should().Be("سرور یکم");
        loaded[untitled.Id].DisplayTitle.Should().Be("Untitled-1");

        (await realm_repo.LoadRealms([])).Should().BeEmpty();

        // an update rewrites the title and null clears it
        loaded[titled.Id].Title = "سرور دوّم";
        await realm_repo.Save(loaded[titled.Id]);

        (await realm_repo.LoadRealms([titled.Id]))[titled.Id].DisplayTitle.Should().Be("سرور دوّم");

        loaded[titled.Id].Title = null;
        await realm_repo.Save(loaded[titled.Id]);

        (await realm_repo.LoadRealms([titled.Id]))[titled.Id]
            .Should().BeEquivalentTo(new { Name = "Titled-01", Title = (string?)null, DisplayTitle = "Titled-01" });
    }
}
