using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class CheckAccess(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Test_invalid_password()
    {
        await Register("user_a_00", "00", "Password");

        await Login("user_a_01", "Password-1", 401);
        await FullInfo("user_a_01", 401);
    }

    [Fact]
    public async Task Test_access_forbidden()
    {
        await Register("user_a_01", "21", "Password");
        await Register("user_a_02", "22", "Password");

        await Login("user_a_01", "Password");
        await FullInfo("user_a_02", 403);
    }

    [Fact]
    public async Task Test_admin()
    {
        await Register("user_a_11", "23", "Password");
        await Register("user_a_12", "24", "Password");

        await Login("admin", "admin");
        await FullInfo("user_a_11");
        await FullInfo("user_a_12");
    }
}