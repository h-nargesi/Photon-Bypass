using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using PhotonBypass.Domain;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Result;
using System.Net.Mime;
using System.Text.Json;

namespace PhotonBypass.Test.Shared;

public class ExceptionHandlingMiddlewareTests
{
    const string error_pr = "خطا";
    const string error_en = "Error Detail";
    private readonly Mock<IJobContext> context;

    public ExceptionHandlingMiddlewareTests()
    {
        context = new Mock<IJobContext>();
        context.Setup(x => x.Username).Returns("User");
    }

    [Fact]
    public async Task Invoke_OK()
    {
        static Task request(HttpContext http) => Task.CompletedTask;

        PrepareResponse(out var http, out var response, out var stream);
        var json = string.Empty;

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        response.StatusCode.Should().Be(StatusCodes.Status200OK);
    }

    [Fact]
    public async Task Invoke_EmptyException()
    {
        static Task request(HttpContext http) => throw new Exception();

        PrepareResponse(out var http, out var response, out var stream);

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 500,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
    }

    [Fact]
    public async Task Invoke_Exception()
    {
        static Task request(HttpContext http) => throw new Exception("Error");

        PrepareResponse(out var http, out var response, out var stream);

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 500,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
    }

    [Fact]
    public async Task Invoke_UserExceptionMessager()
    {
        static Task request(HttpContext http) => throw new UserException(message: error_pr);

        PrepareResponse(out var http, out var response, out var stream);

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = error_pr,
        });
    }

    [Fact]
    public async Task Invoke_UserExceptionDetail()
    {
        static Task request(HttpContext http) => throw new UserException(detail: error_en);

        PrepareResponse(out var http, out var response, out var stream);

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
    }

    [Fact]
    public async Task Invoke_UserException()
    {
        static Task request(HttpContext http) => throw new UserException(error_pr, error_en);

        PrepareResponse(out var http, out var response, out var stream);

        var middleWare = new ExceptionHandlingMiddleware(request, context.Object);
        await middleWare.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = error_pr,
        });
    }

    private static void AssertHttpResponse(HttpResponse response)
    {
        response.StatusCode.Should().Be(StatusCodes.Status500InternalServerError);
        response.ContentType.Should().Be(MediaTypeNames.Application.Json);
    }

    private static async Task AssertStream(MemoryStream stream, ApiResult api)
    {
        stream.Seek(0, SeekOrigin.Begin);
        var reader = new StreamReader(stream);
        var resultText = await reader.ReadToEndAsync();

        resultText.Should().Be(JsonSerializer.Serialize(api));
    }

    private static void PrepareResponse(out HttpContext http, out HttpResponse response, out MemoryStream stream)
    {
        var context = new DefaultHttpContext();

        response = context.Response;
        stream = new MemoryStream();
        response.Body = stream;

        var httpMock = new Mock<HttpContext>();
        httpMock.Setup(x => x.Response).Returns(response);

        http = httpMock.Object;
    }
}
