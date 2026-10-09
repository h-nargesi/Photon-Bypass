using FluentAssertions;
using PhotonBypass.Tools;

namespace PhotonBypass.Test.Facts.BasicFunctions.Shared;

public class HashHandlerTest
{
    private const string EasyChars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz123456789";
    private const string FullChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    [Theory]
    [InlineData(10)]
    [InlineData(56)]
    public void GenerateHashCode_Length(int length)
    {
        HashHandler.GenerateHashCode(length).Length.Should().Be(length);
    }

    [Fact]
    public void GenerateHashCode_EasyCharset()
    {
        var code = HashHandler.GenerateHashCode(1000);

        code.All(EasyChars.Contains).Should().BeTrue();
    }

    [Fact]
    public void GenerateHashCode_FullCharset()
    {
        var code = HashHandler.GenerateHashCode(1000, true);

        code.All(FullChars.Contains).Should().BeTrue();
    }

    [Fact]
    public void GenerateHashCode_ShouldNotRepeat()
    {
        var codes = Enumerable.Range(0, 100)
            .Select(_ => HashHandler.GenerateHashCode(56))
            .ToHashSet();

        codes.Count.Should().Be(100);
    }
}
