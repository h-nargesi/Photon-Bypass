using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using PhotonBypass.Portal.Basical.Captcha;

namespace PhotonBypass.Test.Facts.BasicFunctions.Portal;

public class CaptchaServiceTest : IDisposable
{
    private const string Charset = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    private readonly MemoryCache cache = new(new MemoryCacheOptions());

    public void Dispose() => cache.Dispose();

    private CaptchaService CreateService(int threshold = 3, int expiry_seconds = 180, int code_length = 5)
    {
        return new CaptchaService(cache, Options.Create(new CaptchaOptions
        {
            Enabled = true,
            CodeLength = code_length,
            CodeExpirySeconds = expiry_seconds,
            FailureThreshold = threshold,
            FailureWindowMinutes = 15,
        }));
    }

    private string AnswerOf(CaptchaResult captcha)
    {
        return cache.Get<string>(CaptchaService.CodeKey(captcha.Id))
            ?? throw new Exception("captcha answer missing from cache");
    }

    [Fact]
    public void New_IssuesPngDataUrl_AndCachesCharsetAnswer()
    {
        var service = CreateService(code_length: 5);

        var captcha = service.New();

        captcha.Id.Should().NotBeNullOrWhiteSpace();
        captcha.ExpiresInSeconds.Should().Be(180);
        captcha.Image.Should().StartWith("data:image/png;base64,");

        var answer = AnswerOf(captcha);
        answer.Should().HaveLength(5);
        answer.ToCharArray().Should().OnlyContain(c => Charset.Contains(c));
    }

    [Fact]
    public void New_EveryIssueIsUnique()
    {
        var service = CreateService();

        var first = service.New();
        var second = service.New();

        first.Id.Should().NotBe(second.Id);
        // random rotation/jitter/noise must make two renders of any code differ
        first.Image.Should().NotBe(second.Image);
    }

    [Fact]
    public void Validate_AcceptsCodeCaseInsensitiveAndTrimmed()
    {
        var service = CreateService();
        var captcha = service.New();
        var answer = AnswerOf(captcha);

        service.Validate(captcha.Id, $"  {answer.ToLowerInvariant()} ").Should().BeTrue();
    }

    [Fact]
    public void Validate_IsSingleUse_EvenWhenCorrect()
    {
        var service = CreateService();
        var captcha = service.New();
        var answer = AnswerOf(captcha);

        service.Validate(captcha.Id, answer).Should().BeTrue();
        service.Validate(captcha.Id, answer).Should().BeFalse();
    }

    [Fact]
    public void Validate_WrongCode_ConsumesTheCaptcha()
    {
        var service = CreateService();
        var captcha = service.New();
        var answer = AnswerOf(captcha);

        service.Validate(captcha.Id, "ZZZZZ").Should().BeFalse();
        service.Validate(captcha.Id, answer).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "ABCD")]
    [InlineData("some-id", null)]
    [InlineData("  ", "ABCD")]
    [InlineData("some-id", "   ")]
    public void Validate_MissingIdOrCode_Fails(string? id, string? code)
    {
        CreateService().Validate(id, code).Should().BeFalse();
    }

    [Fact]
    public void Validate_UnknownId_Fails()
    {
        CreateService().Validate("unknown-id", "ABCD").Should().BeFalse();
    }

    [Fact]
    public async Task Validate_AfterExpiry_Fails()
    {
        var service = CreateService(expiry_seconds: 1);
        var captcha = service.New();
        var answer = AnswerOf(captcha);

        await Task.Delay(TimeSpan.FromSeconds(1.2));

        service.Validate(captcha.Id, answer).Should().BeFalse();
    }

    [Fact]
    public void IsRequired_TracksTheAdaptiveThreshold()
    {
        var service = CreateService(threshold: 3);
        const string ip = "1.2.3.4";

        service.IsRequired(ip).Should().BeFalse();

        service.NoteFailure(ip);
        service.NoteFailure(ip);
        service.IsRequired(ip).Should().BeFalse();

        service.NoteFailure(ip);
        service.IsRequired(ip).Should().BeTrue();

        service.NoteSuccess(ip);
        service.IsRequired(ip).Should().BeFalse();
    }

    [Fact]
    public void NoteFailure_IsPartitionedByIp()
    {
        var service = CreateService(threshold: 2);

        service.NoteFailure("1.1.1.1");
        service.NoteFailure("1.1.1.1");

        service.IsRequired("1.1.1.1").Should().BeTrue();
        service.IsRequired("2.2.2.2").Should().BeFalse();
    }

    [Fact]
    public void NoteSuccess_WithoutFailures_IsHarmless()
    {
        var service = CreateService();

        service.NoteSuccess("3.3.3.3");

        service.IsRequired("3.3.3.3").Should().BeFalse();
    }
}
