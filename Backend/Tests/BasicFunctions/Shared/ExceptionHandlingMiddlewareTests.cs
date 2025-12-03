using System.Net.Mime;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using PhotonBypass.Domain;
using PhotonBypass.ErrorHandler;
using PhotonBypass.Portal.Basical;
using PhotonBypass.Result;

namespace PhotonBypass.Test.BasicFunctions.Shared;

public class ExceptionHandlingMiddlewareTests
{
    private const string ErrorPr = "خطا";
    private const string ErrorEn = "Error Detail";
    private readonly Mock<IJobContext> context;

    public ExceptionHandlingMiddlewareTests()
    {
        context = new Mock<IJobContext>();
        context.Setup(x => x.Username).Returns("User");
    }

    [Fact]
    public async Task Invoke_OK()
    {
        PrepareResponse(out var http, out var response, out _);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        response.StatusCode.Should().Be(StatusCodes.Status200OK);
        return;

        static Task Request(HttpContext http) => Task.CompletedTask;
    }

    [Fact]
    public async Task Invoke_EmptyException()
    {
        PrepareResponse(out var http, out var response, out var stream);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 500,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
        return;

        static Task Request(HttpContext http) => throw new Exception();
    }

    [Fact]
    public async Task Invoke_Exception()
    {
        PrepareResponse(out var http, out var response, out var stream);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 500,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
        return;

        static Task Request(HttpContext http) => throw new Exception("Error");
    }

    [Fact]
    public async Task Invoke_UserExceptionMessenger()
    {
        PrepareResponse(out var http, out var response, out var stream);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = ErrorPr,
        });
        return;

        static Task Request(HttpContext http) => throw new UserException(message: ErrorPr);
    }

    [Fact]
    public async Task Invoke_UserExceptionDetail()
    {
        PrepareResponse(out var http, out var response, out var stream);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = ExceptionHandlingMiddleware.ERROR_MESSAGE,
        });
        return;

        static Task Request(HttpContext http) => throw new UserException(detail: ErrorEn);
    }

    [Fact]
    public async Task Invoke_UserException()
    {
        PrepareResponse(out var http, out var response, out var stream);

        var middle_ware = new ExceptionHandlingMiddleware(Request, context.Object);
        await middle_ware.Invoke(http);

        AssertHttpResponse(response);
        await AssertStream(stream, new ApiResult
        {
            Code = 400,
            Message = ErrorPr,
        });
        return;

        static Task Request(HttpContext http) => throw new UserException(ErrorPr, ErrorEn);
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
        var result_text = await reader.ReadToEndAsync();

        result_text.Should().Be(JsonSerializer.Serialize(api));
    }

    private static void PrepareResponse(out HttpContext http, out HttpResponse response, out MemoryStream stream)
    {
        var context = new DefaultHttpContext();

        response = context.Response;
        stream = new MemoryStream();
        response.Body = stream;

        var http_mock = new Mock<HttpContext>();
        http_mock.Setup(x => x.Response).Returns(response);

        http = http_mock.Object;
    }
}
