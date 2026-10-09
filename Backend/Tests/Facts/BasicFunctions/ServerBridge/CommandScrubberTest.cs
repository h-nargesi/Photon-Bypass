using FluentAssertions;
using PhotonBypass.ServerBridge.Ssh;

namespace PhotonBypass.Test.Facts.BasicFunctions.ServerBridge;

public class CommandScrubberTest
{
    [Fact]
    public void Scrub_ExportPassphrase()
    {
        var command =
            "/certificate export-certificate \"CLIENT_user\" export-passphrase=\"secret-pass-123\" file-name=\"CLIENT_user\"";

        var scrubbed = CommandScrubber.Scrub(command);

        scrubbed.Should().Contain("export-passphrase=\"***\"");
        scrubbed.Should().NotContain("secret-pass-123");
    }

    [Fact]
    public void Scrub_PasswordParameter()
    {
        var command = "/tool user-manager user add name=x password=\"p@ss\"";

        CommandScrubber.Scrub(command).Should().Be("/tool user-manager user add name=x password=\"***\"");
    }

    [Fact]
    public void Scrub_CommandWithoutSecret_ShouldNotChange()
    {
        var command = "/system resource print";

        CommandScrubber.Scrub(command).Should().Be(command);
    }
}
