using FluentAssertions;
using PhotonBypass.Application.Account;
using PhotonBypass.Application.Account.Model;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Test.Application.OutSources;

namespace PhotonBypass.Test.Application;

public class AccountAppTest : ServiceInitializer
{
    private readonly PermanentUsersRepositoryMoq permanentUsersRepoMoq;
    private readonly UserPlanStateRepositoryMoq userPlanStateRepoMoq;
    private readonly RadiusServiceMoq radiusServiceMoq;

    public AccountAppTest()
    {
        var builder = Initialize();
        AccountRepositoryMoq.CreateInstance(builder.Services);
        permanentUsersRepoMoq = PermanentUsersRepositoryMoq.CreateInstance(builder.Services);
        userPlanStateRepoMoq = UserPlanStateRepositoryMoq.CreateInstance(builder.Services);
        radiusServiceMoq = RadiusServiceMoq.CreateInstance(builder.Services);
        radiusServiceMoq.PermanentUsersRepoMoq = permanentUsersRepoMoq;
        radiusServiceMoq.UserPlanStateRepoMoq = userPlanStateRepoMoq;
        Build(builder);
    }

    [Fact]
    public async Task GetUser_ValidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();
        var result = await account_app.GetUser("user1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("user1", result.Data.Username);
    }

    [Fact]
    public void GetUser_InvalidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();

        var action = () => account_app.GetUser("invlid username").Wait();

        action.Should().Throw<UserException>();
    }

    [Fact]
    public async Task GetFullInfo_ValidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();
        var result = await account_app.GetFullInfo("user1");

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
        Assert.NotNull(result.Data);
        Assert.Equal("user1", result.Data.Username);
    }

    [Fact]
    public void GetFullInfo_InvalidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();

        var action = () => account_app.GetFullInfo("invlid username").Wait();

        action.Should().Throw<UserException>();
    }

    [Fact]
    public async Task EditUser_ValidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();
        var result = await account_app.EditUser("user1", new EditUserContext
        {
            Firstname = nameof(EditUserContext.Firstname),
            Lastname = nameof(EditUserContext.Lastname),
            Email = nameof(EditUserContext.Email),
            Mobile = nameof(EditUserContext.Mobile),
        });

        Assert.NotNull(result);
        Assert.Equal(2, result.Code / 100);
    }

    [Fact]
    public void EditUser_InvalidUser()
    {
        var account_app = CreateScope().GetRequiredService<IAccountApplication>();

        var action = () => account_app.EditUser("invlid username", new EditUserContext()).Wait();

        action.Should().Throw<UserException>();
    }
}
