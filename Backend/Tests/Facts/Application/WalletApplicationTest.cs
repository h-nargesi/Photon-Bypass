using FluentAssertions;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Billing;
using PhotonBypass.Domain;
using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;

namespace PhotonBypass.Test.Facts.Application;

/// <summary>
/// Admin wallet adjustments and wallet history access (docs/20 phase 3, D5):
/// append-only Completed rows carrying the acting admin, negative balance
/// allowed, and the history access matrix (owner with AllowSelfService or
/// admin — a manager never reads a subordinate's wallet).
/// </summary>
public class WalletApplicationTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task Adjust_RecordsCompletedRowWithActor()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User2");

        var result = await application.Adjust(BalanceDirection.Credit, 500, "  پرداخت حضوری  ");

        Assert.Equal(2, result.Code / 100);

        var row = wallet_moq.Data[2].Single(w => w.Description == "پرداخت حضوری");
        Assert.Equal(BalanceStatus.Completed, row.Status);
        Assert.Equal(BalanceDirection.Credit, row.Direction);
        Assert.Equal(500, row.Amount);
        Assert.Equal(99, row.ActorId);
    }

    [Fact]
    public async Task Adjust_DebitBelowBalance_IsAllowed()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var wallet_repo = scope.ServiceProvider.GetRequiredService<IWalletRepository>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User4");

        var before = await wallet_repo.GetBalance(4);

        // D5: a manual debit may drive the balance negative
        var result = await application.Adjust(BalanceDirection.Debit, before + 1_000, "تصحیح حساب");

        Assert.Equal(2, result.Code / 100);
        Assert.True(await wallet_repo.GetBalance(4) < 0);
    }

    [Fact]
    public async Task Adjust_Validation()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        job_context.InjectJobContext(99, "User99", "User2");

        await Assert.ThrowsAsync<UserException>(() => application.Adjust(0, 100, "d"));
        await Assert.ThrowsAsync<UserException>(() => application.Adjust(BalanceDirection.Credit, 0, "d"));
        await Assert.ThrowsAsync<UserException>(() => application.Adjust(BalanceDirection.Credit, -5, "d"));
        await Assert.ThrowsAsync<UserException>(() => application.Adjust(BalanceDirection.Credit, 100, " "));
        await Assert.ThrowsAsync<UserException>(() => application.Adjust(BalanceDirection.Credit, 100, new string('d', 128)));
    }

    [Fact]
    public async Task Adjust_DependentAdminSelfView_Forbidden()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        // pathological but guarded: an admin account without the self-service
        // flag adjusting its own wallet hits the D6 self gate
        account_moq.Data["User99"].Role = AccountRole.Admin;
        account_moq.Data["User99"].UserType = UserTypes.None;
        job_context.InjectJobContext(99, "User99", "User99");

        var ex = await Assert.ThrowsAsync<UserException>(
            () => application.Adjust(BalanceDirection.Credit, 100, "d"));

        ex.HttpCode.Should().Be(403);
    }

    [Fact]
    public async Task GetHistory_AccessMatrix()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        // self view of an independent account
        job_context.InjectJobContext(2, "User2", "User2");
        (await application.GetHistory(1)).Code.Should().Be(200);

        // self view of a dependent account
        job_context.InjectJobContext(11, "DependentUser11", "DependentUser11");
        (await Assert.ThrowsAsync<UserException>(() => application.GetHistory(1)))
            .HttpCode.Should().Be(403);

        // on-behalf view of a plain user
        job_context.InjectJobContext(4, "User4", "User2");
        (await Assert.ThrowsAsync<UserException>(() => application.GetHistory(1)))
            .HttpCode.Should().Be(403);

        // on-behalf view of a manager over its own subordinate: still refused (docs/20 §3)
        account_moq.Data["User5"].Role = AccountRole.Manager;
        job_context.InjectJobContext(5, "User5", "User6");
        (await Assert.ThrowsAsync<UserException>(() => application.GetHistory(1)))
            .HttpCode.Should().Be(403);

        // on-behalf view of the admin
        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User2");
        (await application.GetHistory(1)).Code.Should().Be(200);
    }

    [Fact]
    public async Task GetHistory_PagingNewestFirstWithActorNames()
    {
        using var scope = App.Services.CreateScope();
        var job_context = scope.ServiceProvider.GetRequiredService<IJobContext>();
        var account_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var wallet_moq = scope.ServiceProvider.GetRequiredService<WalletRepositoryMoq>();
        var application = scope.ServiceProvider.GetRequiredService<IWalletApplication>();

        account_moq.Data["User99"].Role = AccountRole.Admin;
        job_context.InjectJobContext(99, "User99", "User4");

        wallet_moq.Data[4] = Enumerable.Range(1, 25).Select(i => new WalletEntity
        {
            Id = i,
            AccountId = 4,
            Amount = 10 * i,
            Direction = BalanceDirection.Credit,
            Status = BalanceStatus.Completed,
            Description = $"row {i}",
            ActorId = i % 2 == 0 ? 99 : null,
        }).ToList();

        var first = (await application.GetHistory(1)).Data!;

        Assert.Equal(25, first.TotalCount);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(20, first.Items.Count);
        Assert.Equal(25, first.Items[0].Id); // newest first
        Assert.Equal(6, first.Items[^1].Id);

        var admin_row = first.Items.Single(i => i.Id == 24);
        Assert.Equal("User99", admin_row.Actor);
        Assert.Null(first.Items.Single(i => i.Id == 23).Actor);

        var second = (await application.GetHistory(2)).Data!;

        Assert.Equal(5, second.Items.Count);
        Assert.Equal(5, second.Items[0].Id);
        Assert.Equal(1, second.Items[^1].Id); // oldest row closes the last page

        await Assert.ThrowsAsync<UserException>(() => application.GetHistory(0));
    }
}
