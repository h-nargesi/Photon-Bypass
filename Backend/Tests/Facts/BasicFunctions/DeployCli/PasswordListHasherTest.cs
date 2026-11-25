using FluentAssertions;
using PhotonBypass.DeployCli;
using PhotonBypass.Domain.Account.Business;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.DeployCli;

public class PasswordListHasherTest
{
    private const string Valid = "Passw0rd1";
    private const string Other = "OtherPass2";
    private const string Persian = "رمزعبور۱آزمایش";

    [Fact]
    public void Hash_ValidLines_RoundtripAndIndependentSalt()
    {
        var first = PasswordListHasher.Hash([Valid, Other, Persian]);
        var second = PasswordListHasher.Hash([Valid]);

        first.Succeeded.Should().BeTrue();
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

    /// <summary>
    /// The portal password policy does not apply to batch passwords: every value
    /// <see cref="AccountBusiness.ValidatePassword"/> rejects is hashed anyway.
    /// </summary>
    [Theory]
    [InlineData("plaintext-secret")]
    [InlineData("a1")]
    [InlineData("12345678")]
    [InlineData("رمزعبور")]
    public void Hash_PasswordOutsideThePortalPolicy_IsHashedAnyway(string password)
    {
        ((Action)(() => AccountBusiness.ValidatePassword(password))).Should().Throw<UserException>();

        var batch = PasswordListHasher.Hash([Valid, password, Other]);

        batch.Succeeded.Should().BeTrue();
        batch.Hashes.Should().HaveCount(3);
        PasswordHasher.Verify(Valid, batch.Hashes[0]!).Should().BeTrue();
        PasswordHasher.Verify(password, batch.Hashes[1]!).Should().BeTrue();
        PasswordHasher.Verify(Other, batch.Hashes[2]!).Should().BeTrue();
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
    public void Hash_WhitespaceOnlyLine_IsAPasswordNotABlankLine()
    {
        var batch = PasswordListHasher.Hash(["   "]);

        batch.Succeeded.Should().BeTrue();
        batch.Hashes.Should().HaveCount(1);
        PasswordHasher.Verify("   ", batch.Hashes[0]!).Should().BeTrue();
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
    }
}
