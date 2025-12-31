using PhotonBypass.Test.Initializer;

namespace PhotonBypass.Test.Facts.Scenario;

public class CheckAccess(ProgramLevelInitializer.Factory factory) : HttpHandler(factory)
{
    [Fact]
    public async Task Test()
    {
        await Register("user_a_01", "21");
        await Register("user_a_02", "22");

        await Login("user_a_01", "Password");
        await FullInfo("user_a_01", 4);
    }

    [Fact]
    public async Task Test_admin()
    {
        await Register("user_a_11", "23");
        await Register("user_a_12", "24");

        await Login("admin", "admin");
        await FullInfo("user_a_11");
        await FullInfo("user_a_12");
    }
}