using FluentAssertions;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.ServerBridge.Ssh;
using PhotonBypass.ServerBridge.Tunnel;
using OperatingSystem = PhotonBypass.Domain.Servers.Types.OperatingSystem;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

/// <summary>
/// The command verdict shared by both transports (Docs/25 D10): stderr, then
/// a positive exit status, then — on RouterOS only — an error text on stdout.
/// </summary>
public class CommandOutcomeTest
{
    [Theory]
    [InlineData(OperatingSystem.Mikrotik, 0)]
    [InlineData(OperatingSystem.Mikrotik, -1)]
    [InlineData(OperatingSystem.Ubuntu, 0)]
    [InlineData(OperatingSystem.Ubuntu, -1)]
    public void Stderr_WinsOverEverything_ResultIsTheStderrText(OperatingSystem os, int exit_status)
    {
        var verdict = CommandOutcome.Classify(new SshCommandResult(exit_status, "partial output", "boom on stderr"), os);

        verdict.Success.Should().BeFalse();
        verdict.Result.Should().Be("boom on stderr");
    }

    [Theory]
    [InlineData(OperatingSystem.Mikrotik)]
    [InlineData(OperatingSystem.Ubuntu)]
    public void PositiveExitStatus_Fails_WithTheOutputAsResult(OperatingSystem os)
    {
        var verdict = CommandOutcome.Classify(new SshCommandResult(2, "something went wrong", ""), os);

        verdict.Success.Should().BeFalse();
        verdict.Result.Should().Be("something went wrong");
    }

    [Theory]
    [InlineData(OperatingSystem.Mikrotik)]
    [InlineData(OperatingSystem.Ubuntu)]
    public void PositiveExitStatus_WithoutOutput_ReportsTheStatusInsteadOfAnEmptyError(OperatingSystem os)
    {
        var verdict = CommandOutcome.Classify(new SshCommandResult(127, "   ", ""), os);

        verdict.Success.Should().BeFalse();
        verdict.Result.Should().Be("exit-status 127");
    }

    [Theory]
    [InlineData("failure: already have such name")]
    [InlineData("expected end of command (line 1 column 9)")]
    [InlineData("not enough permissions (line 1 column 5)")]
    [InlineData("no such item")]
    [InlineData("bad command name x (line 1 column 1)")]
    [InlineData("syntax error (line 1 column 5)")]
    public void RouterOsErrorTextOnStdout_FailsOnMikrotik_WithTheOutputAsResult(string output)
    {
        var verdict = CommandOutcome.Classify(new SshCommandResult(-1, output, ""), OperatingSystem.Mikrotik);

        verdict.Success.Should().BeFalse();
        verdict.Result.Should().Be(output);
    }

    [Theory]
    [InlineData("failure: already have such name")]
    [InlineData("no such item")]
    [InlineData("syntax error (line 1 column 5)")]
    public void RouterOsErrorTextOnStdout_IsOnlyARouterOsRule(string output)
    {
        var verdict = CommandOutcome.Classify(new SshCommandResult(-1, output, ""), OperatingSystem.Ubuntu);

        verdict.Success.Should().BeTrue("the RouterOS wording means nothing on another system");
        verdict.Result.Should().Be(output);
    }

    [Fact]
    public void NoExitStatus_AndNoError_IsASuccess_EvenWithAnEmptyOutput()
    {
        var empty = CommandOutcome.Classify(new SshCommandResult(-1, "", ""), OperatingSystem.Mikrotik);
        var printed = CommandOutcome.Classify(new SshCommandResult(0, "name=\"CLIENT_user_1\"", ""), OperatingSystem.Mikrotik);

        empty.Success.Should().BeTrue();
        empty.Result.Should().BeEmpty();
        printed.Success.Should().BeTrue();
        printed.Result.Should().Be("name=\"CLIENT_user_1\"");
    }

    [Fact]
    public void RouterOsErrorPatterns_DoNotMatchOrdinaryPrintOutput()
    {
        const string print = "name=\"CLIENT_user_1\" comment=\"no such item left\" invalid=\"syntax error?\"\n";

        RouterOsErrorPatterns.IsAny(print).Should().BeFalse("a value that merely contains error-like words is still print output");
        RouterOsErrorPatterns.IsAny("done\nno such item\n").Should().BeTrue("the anchor is the line start, not the whole output");
    }
}
