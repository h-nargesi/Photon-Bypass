using FluentAssertions;
using PhotonBypass.DeployCli;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.DeployCli;

public class PasswordListHasherTest
{
    private const string Valid = "Passw0rd1";
    private const string Other = "OtherPass2";
    private const string Persian = "رمزعبور۱آزمایش";
    private const string Weak = "plaintext-secret";

    [Fact]
    public void Hash_ValidLines_RoundtripAndIndependentSalt()
    {
        var first = PasswordListHasher.Hash([Valid, Other, Persian]);
        var second = PasswordListHasher.Hash([Valid]);

        first.Succeeded.Should().BeTrue();
        first.InvalidLines.Should().BeEmpty();
        first.Hashes.Should().HaveCount(3);
        first.Hashes.Should().AllSatisfy(hash => hash.Should().StartWith("$PBKDF2$v1$210000$"));

        PasswordHasher.Verify(Valid, first.Hashes[0]!).Should().BeTrue();
        PasswordHasher.Verify(Other, first.Hashes[1]!).Should().BeTrue();
        PasswordHasher.Verify(Persian, first.Hashes[2]!).Should().BeTrue();
        PasswordHasher.Verify(Valid, second.Hashes[0]!).Should().BeTrue();
        first.Hashes[0].Should().NotBe(second.Hashes[0]);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        var batch = PasswordListHasher.Hash([Valid, Valid]);

        batch.Succeeded.Should().BeTrue();
        batch.Hashes[0].Should().NotBe(batch.Hashes[1]);
        PasswordHasher.Verify(Valid, batch.Hashes[0]!).Should().BeTrue();
        PasswordHasher.Verify(Valid, batch.Hashes[1]!).Should().BeTrue();
    }

    [Fact]
    public void Hash_BlankLines_StayAligned()
    {
        var batch = PasswordListHasher.Hash(["", Valid, "", Other, ""]);

        batch.Succeeded.Should().BeTrue();
        batch.Hashes.Should().Equal(null, batch.Hashes[1], null, batch.Hashes[3], null);
        PasswordHasher.Verify(Valid, batch.Hashes[1]!).Should().BeTrue();
        PasswordHasher.Verify(Other, batch.Hashes[3]!).Should().BeTrue();
    }

    [Fact]
    public void Hash_DoesNotTrim()
    {
        const string padded = "Passw0rd1 ";

        var batch = PasswordListHasher.Hash([padded]);

        batch.Succeeded.Should().BeTrue();
        PasswordHasher.Verify(padded, batch.Hashes[0]!).Should().BeTrue();
        PasswordHasher.Verify(Valid, batch.Hashes[0]!).Should().BeFalse();
    }

    [Fact]
    public void Hash_InvalidLine_RejectsTheWholeBatchWithoutHashes()
    {
        var batch = PasswordListHasher.Hash([Valid, Weak, Other]);

        batch.Succeeded.Should().BeFalse();
        batch.Hashes.Should().BeEmpty();
        batch.InvalidLines.Should().ContainSingle();
        batch.InvalidLines[0].LineNumber.Should().Be(2);
        batch.InvalidLines[0].Message.Should().NotContain(Weak);
        batch.InvalidLines[0].Message.Should().NotContain(Valid);
    }

    [Fact]
    public void Hash_SeveralInvalidLines_ReportsEveryLineNumber()
    {
        var batch = PasswordListHasher.Hash([Valid, Weak, "", "   ", Other]);

        batch.Succeeded.Should().BeFalse();
        batch.Hashes.Should().BeEmpty();
        batch.InvalidLines.Select(line => line.LineNumber).Should().Equal(2, 4);
        batch.InvalidLines.Should().AllSatisfy(line =>
        {
            line.Message.Should().NotContain(Weak);
            line.Message.Should().NotContain("   ");
        });
    }

    [Theory]
    [InlineData()]
    [InlineData("")]
    [InlineData("", "")]
    public void Hash_NoPasswords_FailsWithNoHashes(params string[] lines)
    {
        var batch = PasswordListHasher.Hash(lines);

        batch.Succeeded.Should().BeFalse();
        batch.Hashes.Should().BeEmpty();
        batch.InvalidLines.Should().BeEmpty();
    }
}
