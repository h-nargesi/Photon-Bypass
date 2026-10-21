using FluentAssertions;
using PhotonBypass.Domain.Account.Entity;
using PhotonBypass.Domain.Account.Model;
using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Domain.Plan.Entity;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.Test.Initializer;
using PhotonBypass.Test.Mock.MockLocalRepository;
using PhotonBypass.Test.Mock.MockOutSource;
using PhotonBypass.Test.Mock.MockServerBridge;

namespace PhotonBypass.Test.Facts.Application;

public class AccountMonitoringServiceTest : UnitLevelServiceInitializer
{
    [Fact]
    public async Task InactiveAbandonedUsers_Check()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4_net_moq = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var user_data_dictionary = tik4_net_moq.GetData<UserModel>()
            .ToDictionary(user => user.Id ?? string.Empty, user => user.Name);
        var user_profile_data_dictionary = tik4_net_moq.GetData<UserProfileModel>()
            .ToDictionary(profile => profile.Id ?? string.Empty, profile => profile.Username);
        var session_data_dictionary = tik4_net_moq.GetData<SessionModel>()
            .ToDictionary(session => session.Id ?? string.Empty, session => session.Username);

        var other = false;
        var user_4_actions = new bool[4];
        var user_3_actions = new bool[1];

        var plan_state_list = await plan_state_repo.GetAll();
        tik4_net_moq.OnExecute += (command_text, parameters) =>
        {
            var action_types = command_text.Split('/').Skip(1).ToArray();

            if (action_types.Length < 2)
            {
                other = true;
                return;
            }

            Dictionary<string, string>? username_dictionary;
            int index;
            switch (action_types[1])
            {
                case "user":
                    index = 0;
                    username_dictionary = user_data_dictionary;
                    break;
                case "user-profile":
                    index = 1;
                    username_dictionary = user_profile_data_dictionary;
                    break;
                case "session":
                    index = 2;
                    username_dictionary = session_data_dictionary;
                    break;
                default:
                    other = true;
                    return;
            }

            var id = parameters.Where(parameter => parameter.Name == ".id")
                .Select(parameter => parameter.Value)
                .FirstOrDefault();

            if (id == null)
            {
                other = true;
                return;
            }

            var username = username_dictionary[id];

            if (command_text.EndsWith("/remove") && username == "User4")
            {
                user_4_actions[index] = true;
            }
            else if (command_text.EndsWith("/set") && username == "User3")
            {
                user_3_actions[index] = true;
            }
            else
            {
                other = true;
            }
        };

        account_repo_moq.OnSetDeactivated += account_ids =>
        {
            var saved_list = account_ids.ToList();

            if (saved_list.Count != 1 || saved_list[0] != account_repo_moq.Data["User4"].Id)
            {
                other = true;
                return;
            }

            user_4_actions[3] = true;
        };

        await monitoring.InactiveAbandonedUsers(plan_state_list);

        // session for user-4 does not exist, so it should be false
        user_4_actions[2] = !user_4_actions[2];

        // do not remove user-profile anymore
        user_4_actions[1] = !user_4_actions[1];

        foreach (var is_done in user_4_actions)
            Assert.True(is_done);

        foreach (var is_done in user_3_actions)
            Assert.True(is_done);

        Assert.False(other);
    }

    [Fact]
    public async Task NotifSendServices_Check()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_service_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var plan_state_list = await plan_state_repo.GetAll();
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();

        var emails = new HashSet<string> { "User99@gmail.com", "User4@gmail.com" };
        email_service_moq.OnSend += mail =>
        {
            Assert.Contains(mail.ToAddress, emails);
        };

        account_repo_moq.OnUpdateWarningTimes += account_ids =>
        {
            var expected_ids = new HashSet<int>(
                finishing_list
                    .Select(plan => account_repo_moq.Data.GetValueOrDefault(plan.Username))
                    .Where(account => account is { IsEmailValid: true })
                    .Select(account => account!.Id));

            Assert.NotEmpty(account_ids);
            Assert.Subset(expected_ids, account_ids.ToHashSet());
        };

        await monitoring.NotifSendServices(finishing_list);
    }

    [Fact]
    public async Task NotifSendServices_TelegramFirst_WinsOverEmailAndStamps()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var social_moq = scope.ServiceProvider.GetRequiredService<SocialMediaServiceMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        // D5 priority: User4 owns BOTH a validated email and a connected
        // social chat — the social channel must win; User3 stays email-only
        account_repo_moq.Data["User3"].IsEmailValid = true;
        account_repo_moq.Data["User4"].IsEmailValid = true;
        account_repo_moq.Data["User4"].TelegramChatId = 555;
        social_moq.SupportsAccounts = true;

        var finishing_list = (await plan_state_repo.GetAll()).Where(plan => plan.IsFinishing()).ToList();

        var alerted_accounts = new List<AccountEntity>();
        social_moq.OnFinishServiceAlert += account => alerted_accounts.Add(account);

        var email_send_count = 0;
        email_moq.OnSend += _ => email_send_count++;

        await monitoring.NotifSendServices(finishing_list);

        alerted_accounts.Should().Contain(account => account.Username == "User4");

        // the social branch keeps the stamp-after-success bookkeeping
        account_repo_moq.Data["User4"].LastWarningTime.Should().NotBeNull();
        history_moq.Data.Should().Contain(history =>
            history.Title == "پایان پلن" && history.Target == account_repo_moq.Data["User4"].Id);

        // the email-only accounts still went through the email path
        email_send_count.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task NotifSendServices_OneSocialSendFails_OnlySuccessfulAccountsAreStamped()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var social_moq = scope.ServiceProvider.GetRequiredService<SocialMediaServiceMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        account_repo_moq.Data["User3"].IsEmailValid = true;
        account_repo_moq.Data["User4"].IsEmailValid = true;
        account_repo_moq.Data["User4"].TelegramChatId = 555;
        social_moq.SupportsAccounts = true;

        // the social send faults for one account while the email path keeps
        // working for the rest of the batch
        social_moq.OnFinishServiceAlert += account =>
        {
            if (account.Username == "User4")
            {
                throw new InvalidOperationException("bot unreachable");
            }
        };

        var finishing_list = (await plan_state_repo.GetAll()).Where(plan => plan.IsFinishing()).ToList();

        var function = () => monitoring.NotifSendServices(finishing_list);

        await function.Should().NotThrowAsync();

        var user3 = account_repo_moq.Data["User3"];
        var user4 = account_repo_moq.Data["User4"];

        user3.LastWarningTime.Should().NotBeNull("the email account succeeded and must be stamped");
        user4.LastWarningTime.Should().BeNull("the failed social send stays unstamped for the hourly retry");
    }

    [Fact]
    public async Task ParallelMonitoring_WarningTimeAndInactiveBothApplied()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var plan_state_list = await plan_state_repo.GetAll();

        // User4 finishes its plan and hits the delete-inactivity threshold at the
        // same time: NotifSendServices stamps LastWarningTime while
        // InactiveAbandonedUsers marks the account Deactivated.
        account_repo_moq.Data["User4"].IsEmailValid = true;

        var user4_id = account_repo_moq.Data["User4"].Id;

        var warned_ids = new HashSet<int>();
        var inactive_ids = new HashSet<int>();

        account_repo_moq.OnUpdateWarningTimes += ids => warned_ids.UnionWith(ids);
        account_repo_moq.OnSetDeactivated += ids => inactive_ids.UnionWith(ids);

        await Task.WhenAll(
            monitoring.NotifSendServices(plan_state_list),
            monitoring.InactiveAbandonedUsers(plan_state_list));

        // H2: the two parallel jobs write different columns, so both land
        // instead of a last-writer-wins full-entity overwrite.
        Assert.Contains(user4_id, warned_ids);
        Assert.Contains(user4_id, inactive_ids);

        Assert.NotNull(account_repo_moq.Data["User4"].LastWarningTime);
        Assert.Equal(AccountStatus.Deactivated, account_repo_moq.Data["User4"].Status);
    }

    [Fact]
    public async Task NotifSendServices_MissingAccount_SkipsWithoutThrow()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var missing_id = account_repo_moq.Data.Values.Max(account => account.Id) + 1000;

        var ghost_plan = new PlanStateEntity
        {
            Id = missing_id,
            Username = "GhostUser",
            SimultaneousUser = 1,
            TrafficLeft = 1,
            TrafficLimit = 1000,
        };

        var function = () => monitoring.NotifSendServices([ghost_plan]);

        await function.Should().NotThrowAsync();
    }

    [Fact]
    public async Task NotifSendServices_OneSendFails_OnlySuccessfulAccountsAreStamped()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        account_repo_moq.Data["User3"].IsEmailValid = true;
        account_repo_moq.Data["User4"].IsEmailValid = true;

        var plan_state_list = await plan_state_repo.GetAll();
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();

        email_moq.OnSend += mail =>
        {
            if (mail.ToAddress == "user4@gmail.com")
            {
                throw new InvalidOperationException("smtp refused the recipient");
            }
        };

        var function = () => monitoring.NotifSendServices(finishing_list);

        // a failing send must never fail the whole hourly job
        await function.Should().NotThrowAsync();

        var user3 = account_repo_moq.Data["User3"];
        var user4 = account_repo_moq.Data["User4"];

        // stamp-after-success: the delivered warning stamps its account, the
        // failed one stays unstamped so the next hourly run retries it
        user3.LastWarningTime.Should().NotBeNull();
        user4.LastWarningTime.Should().BeNull();

        var warn_targets = history_moq.Data
            .Where(history => history.Title == "پایان پلن")
            .Select(history => history.Target)
            .ToHashSet();

        warn_targets.Should().Contain(user3.Id);
        warn_targets.Should().NotContain(user4.Id);
    }

    [Fact]
    public async Task NotifSendServices_NoReachableChannel_NoStampNoSendNoHistory()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        var plan_state_list = await plan_state_repo.GetAll();
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();

        // no finishing account carries a validated email (and this build has
        // no SOCIAL channel): the whole batch has no reachable channel
        finishing_list.Select(plan => account_repo_moq.Data.GetValueOrDefault(plan.Username)?.EmailAddress)
            .Should().OnlyContain(address => address == null);

        var send_count = 0;
        var stamped = false;
        email_moq.OnSend += _ => send_count++;
        account_repo_moq.OnUpdateWarningTimes += _ => stamped = true;

        var function = () => monitoring.NotifSendServices(finishing_list);

        await function.Should().NotThrowAsync();

        send_count.Should().Be(0);
        stamped.Should().BeFalse();
        history_moq.Data.Should().NotContain(history => history.Title == "پایان پلن");
    }

    [Fact]
    public async Task NotifSendServices_UserOptOut_NoSendNoStampNoHistory()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        // User4 finishes its plan but opted out of the finishing warnings
        account_repo_moq.Data["User4"].IsEmailValid = true;
        account_repo_moq.Data["User4"].SendWarning = false;

        var finishing_list = (await plan_state_repo.GetAll()).Where(plan => plan.IsFinishing()).ToList();

        var send_count = 0;
        var stamped = false;
        email_moq.OnSend += _ => send_count++;
        account_repo_moq.OnUpdateWarningTimes += _ => stamped = true;

        await monitoring.NotifSendServices(finishing_list);

        send_count.Should().Be(0);
        stamped.Should().BeFalse();
        history_moq.Data.Should().NotContain(history => history.Title == "پایان پلن");
    }

    [Fact]
    public async Task NotifSendServices_RecentlyWarnedAccount_IsSkippedByTheThrottle()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();
        var account_repo_moq = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();
        var history_moq = scope.ServiceProvider.GetRequiredService<HistoryRepositoryMoq>();

        // User4 was warned minutes ago: inside the DelayBetweenWarnings window
        account_repo_moq.Data["User4"].IsEmailValid = true;
        account_repo_moq.Data["User4"].LastWarningTime = DateTime.Now.AddMinutes(-10);

        var finishing_list = (await plan_state_repo.GetAll()).Where(plan => plan.IsFinishing()).ToList();

        var send_count = 0;
        var stamped = false;
        email_moq.OnSend += _ => send_count++;
        account_repo_moq.OnUpdateWarningTimes += _ => stamped = true;

        await monitoring.NotifSendServices(finishing_list);

        // the throttle skips the account before any dispatch is built
        send_count.Should().Be(0);
        stamped.Should().BeFalse();
        history_moq.Data.Should().NotContain(history => history.Title == "پایان پلن");
    }
}