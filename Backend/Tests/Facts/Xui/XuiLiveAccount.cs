using PhotonBypass.Domain.Account;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Domain.Servers;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Initializer.OutSourceManager;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Boots both outsourced dependencies of the live x-ui sync tier in one
/// scope: the <c>FastBypass_XuiLive</c> test database (structure only — all
/// rows are seeded per test through the real repositories) and the Xui-Test
/// VM (snapshot restore once per run, Docs/23 D4).
/// </summary>
internal static class XuiLiveHarness
{
    public const string DatabaseKey = "XuiLive";

    public static async Task Register(IServiceScope scope, object service)
    {
        await scope.Register<LocalDatabaseInitializer>(DatabaseKey, service);
        await scope.Register<XuiPanelInitializer>(XuiPanelInitializer.VmKey, service);
    }
}

/// <summary>
/// Per-test database fixture for the live x-ui sync tests: a dedicated realm
/// (fresh sync lock) + active Xui server row pointing at the VM, plus the
/// account chain (panel user → wallet → vpn account → renewal) whose username
/// matches the panel client email — the collector's join key. Everything is
/// removed again in <see cref="DisposeAsync"/>.
/// </summary>
internal sealed class XuiLiveAccount : IAsyncDisposable
{
    private readonly IRealmRepository realm_repo;
    private readonly IServerRepository server_repo;
    private readonly IPanelUserRepository user_repo;
    private readonly IWalletRepository wallet_repo;
    private readonly IVpnAccountRepository account_repo;
    private readonly IRenewalRepository renewal_repo;
    private readonly IPlanRepository plan_repo;

    private PanelUserEntity user = null!;
    private WalletEntity wallet = null!;
    private PlanEntity plan = null!;

    private XuiLiveAccount(
        IRealmRepository realm_repo, IServerRepository server_repo, IPanelUserRepository user_repo,
        IWalletRepository wallet_repo, IVpnAccountRepository account_repo, IRenewalRepository renewal_repo,
        IPlanRepository plan_repo)
    {
        this.realm_repo = realm_repo;
        this.server_repo = server_repo;
        this.user_repo = user_repo;
        this.wallet_repo = wallet_repo;
        this.account_repo = account_repo;
        this.renewal_repo = renewal_repo;
        this.plan_repo = plan_repo;
    }

    public RealmEntity Realm { get; private set; } = null!;

    public ServerEntity Server { get; private set; } = null!;

    public VpnAccountEntity Account { get; private set; } = null!;

    public RenewalEntity Renewal { get; private set; } = null!;

    public static async Task<XuiLiveAccount> CreateAsync(IServiceProvider services, string email)
    {
        var fixture = new XuiLiveAccount(
            services.GetRequiredService<IRealmRepository>(),
            services.GetRequiredService<IServerRepository>(),
            services.GetRequiredService<IPanelUserRepository>(),
            services.GetRequiredService<IWalletRepository>(),
            services.GetRequiredService<IVpnAccountRepository>(),
            services.GetRequiredService<IRenewalRepository>(),
            services.GetRequiredService<IPlanRepository>());

        // unique realm per test: LastTrafficSync NULL = the collector's sync
        // lock is guaranteed acquirable no matter how fresh the previous test
        var suffix = email[(email.LastIndexOf('.') + 1)..];

        fixture.Realm = new RealmEntity { IsActive = true, Name = $"It{suffix}" };
        await fixture.realm_repo.Save(fixture.Realm);

        fixture.Server = XuiPanelTestServer.BuildDbRow(fixture.Realm.Id);
        await fixture.server_repo.Save(fixture.Server);

        // Docs/24 D6: the member joins the default group (structure-seeded)
        var group_repo = services.GetRequiredService<IUserGroupRepository>();

        fixture.user = new PanelUserEntity
        {
            Username = email,
            Password = "T-xui-live",
            UserGroupId = (await group_repo.GetDefault()).Id,
        };
        await fixture.user_repo.Save(fixture.user);

        // one debit wallet row anchors the renewal's NOT NULL WalletDebit FK
        fixture.wallet = new WalletEntity
        {
            AccountId = fixture.user.Id,
            Amount = 1,
            Direction = BalanceDirection.Debit,
            Status = BalanceStatus.Completed,
            Description = "T-xui-live",
        };
        await fixture.wallet_repo.Save(fixture.wallet);

        fixture.Account = new VpnAccountEntity
        {
            UserId = fixture.user.Id,
            RealmId = fixture.Realm.Id,
            Username = email,
            VpnPassword = "T-xui-live",
        };
        await fixture.account_repo.Save(fixture.Account);

        // Docs/24 §3: renewals carry a NOT NULL plan reference — a minimal
        // fixture plan per test (the harness DB is structure-only)
        fixture.plan = new PlanEntity
        {
            Name = $"T-xui-live-{suffix}",
            Title = "T-xui-live",
            IsActive = true,
            GroupId = fixture.user.UserGroupId,
            UsersBase = 1,
            UsersCap = 6,
            CalculatorCode =
                "using PhotonBypass.Domain.Static; public class Calculator { public static PriceCalculationOutput Compute(PriceCalculationInput input) => new PriceCalculationOutput { Price = 1 }; }",
        };
        await fixture.plan_repo.Save(fixture.plan);

        fixture.Renewal = new RenewalEntity
        {
            VpnAccountId = fixture.Account.Id,
            PlanId = fixture.plan.Id,
            SimultaneousUser = 1,
            WalletDebit = fixture.wallet.Id,
            Comment = "T-xui-live",
        };
        await fixture.renewal_repo.Save(fixture.Renewal);

        return fixture;
    }

    /// <summary>Releases the realm's traffic-sync lock between two collector runs.</summary>
    public async Task ReleaseSyncLockAsync()
    {
        Realm.LastTrafficSync = null;
        await realm_repo.Save(Realm);
    }

    public async ValueTask DisposeAsync()
    {
        await renewal_repo.Delete(Renewal);
        await account_repo.Delete(Account);

        // the renewal is gone — the plan row is unreferenced and follows it
        await plan_repo.Delete(plan);

        await wallet_repo.Delete(wallet);
        await user_repo.Delete(user);
        await server_repo.Delete(Server);
        await realm_repo.Delete(Realm);
    }
}
