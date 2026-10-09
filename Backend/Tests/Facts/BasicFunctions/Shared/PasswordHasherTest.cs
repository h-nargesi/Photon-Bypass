using FluentAssertions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.Shared;

public class PasswordHasherTest
{
    [Theory]
    [InlineData("abc")]
    [InlineData("P@ssw0rd-تست")]
    [InlineData("")]
    public void Hash_Verify_Roundtrip(string password)
    {
        var hash = PasswordHasher.Hash(password);

        hash.Should().StartWith("$PBKDF2$v1$210000$");
        PasswordHasher.Verify(password, hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_WrongPassword_ShouldFail()
    {
        var hash = PasswordHasher.Hash("abc");

        PasswordHasher.Verify("abd", hash).Should().BeFalse();
    }

    [Fact]
    public void Hash_SamePassword_ShouldProduceDifferentHash()
    {
        var first = PasswordHasher.Hash("abc");
        var second = PasswordHasher.Hash("abc");

        first.Should().NotBe(second);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("$PBKDF2$v1$abc$c2FsdA==$aGFzaA==")]
    [InlineData("$PBKDF2$v1$210000$!!invalid-b64!!$aGFzaA==")]
    [InlineData("$ARGON2$v1$210000$c2FsdA==$aGFzaA==")]
    public void Verify_MalformedStored_ShouldReturnFalse(string? stored)
    {
        PasswordHasher.Verify("abc", stored!).Should().BeFalse();
    }
}
