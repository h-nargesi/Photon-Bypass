using PhotonBypass.Domain.Management;
using PhotonBypass.Domain.OutSource;
using PhotonBypass.Domain.Plan;
using PhotonBypass.Domain.Plan.Business;
using PhotonBypass.Mikrotik.Radius.Model;
using PhotonBypass.Test.MockLocalRepository;
using PhotonBypass.Test.MockServerBridge;

namespace PhotonBypass.Test.Application;

public class AccountMonitoringServiceTest : ServiceInitializer
{
    [Fact]
    public async Task InactiveAbandonedUsers_Check()
    {
        using var scope = App.Services.CreateScope();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var tik4_net = scope.ServiceProvider.GetRequiredService<Tik4NetHandlerMoq>();
        var account_repo = scope.ServiceProvider.GetRequiredService<AccountRepositoryMoq>();

        var user_data_dictionary = tik4_net.GetData<UserModel>()
            .ToDictionary(user => user.Id, user => user.Name);
        var user_profile_data_dictionary = tik4_net.GetData<UserProfileModel>()
            .ToDictionary(profile => profile.Id, profile => profile.Username);
        var session_data_dictionary = tik4_net.GetData<SessionModel>()
            .ToDictionary(session => session.Id, session => session.Username);

        var other = false;
        var user_4_actions = new bool[4];
        var user_3_actions = new bool[1];

        var plan_state_list = await plan_state_repo.GetAll();
        tik4_net.OnExecute += (command_text, parameters) =>
        {
            var action_types = command_text.Split('/').Skip(1).ToArray();

            if (action_types.Length < 2)
            {
                other = true;
                return;
            }

            Dictionary<int, string?> username_dictionary;
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

            var username = username_dictionary[int.Parse(id)];

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

        account_repo.OnSave += account =>
        {
            if (account is { Username: "User4", Active: false })
            {
                user_4_actions[3] = true;
            }
            else
            {
                other = true;
            }
        };

        await monitoring.InactiveAbandonedUsers(plan_state_list);

        // session for user-4 does not exist, so it should be false
        user_4_actions[2] = !user_4_actions[2];

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

        scope.ServiceProvider.GetRequiredService<IServerManagementService>();
        scope.ServiceProvider.GetRequiredService<ISocialMediaService>();

        var monitoring = scope.ServiceProvider.GetRequiredService<IAccountMonitoringService>();
        var plan_state_repo = scope.ServiceProvider.GetRequiredService<IPlanStateRepository>();
        var email_service_moq = scope.ServiceProvider.GetRequiredService<EmailHandlerMoq>();

        var plan_state_list = await plan_state_repo.GetAll();
        var finishing_list = plan_state_list.Where(plan => plan.IsFinishing()).ToList();

        var emails = new HashSet<string>() { "User1", "User4" };
        email_service_moq.OnSend += (mail) =>
        {
            // mail.To.Contains()
            // Assert.Contains(username, emails);
        };

        await monitoring.NotifSendServices(finishing_list);
    }
}