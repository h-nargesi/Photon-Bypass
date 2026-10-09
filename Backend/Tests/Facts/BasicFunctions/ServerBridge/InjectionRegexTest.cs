using FluentAssertions;
using PhotonBypass.ServerBridge;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

public class InjectionRegexTest
{
    [Theory]
    [InlineData("0F52A7", true)]
    [InlineData("0f52a7", true)]
    [InlineData("", false)]
    [InlineData("0F52A7\n", false)]
    [InlineData("0F52A7\r", false)]
    [InlineData("2036A3\n/reboot", false)]
    [InlineData("2036 A3", false)]
    [InlineData("2036A3;reboot", false)]
    public void SessionId_Anchor(string value, bool expected)
    {
        InjectionRegex.SessionId().IsMatch(value).Should().Be(expected);
    }

    [Theory]
    [InlineData("user_1-x.y", true)]
    [InlineData("USER.99", true)]
    [InlineData("", false)]
    [InlineData("user 1", false)]
    [InlineData("user\n", false)]
    [InlineData("کاربر", false)]
    [InlineData("user;reboot", false)]
    public void Username_Anchor(string value, bool expected)
    {
        InjectionRegex.Username().IsMatch(value).Should().Be(expected);
    }
}
