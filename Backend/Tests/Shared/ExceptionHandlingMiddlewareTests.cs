using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using PhotonBypass.Domain;
using PhotonBypass.ErrorHandler;
using System.Net.Mime;

namespace PhotonBypass.Test.Shared;

public class ExceptionHandlingMiddlewareTests
{
    private readonly Mock<IJobContext> context;

    public ExceptionHandlingMiddlewareTests()
    {
        context = new Mock<IJobContext>();
        context.Setup(x => x.Username).Returns("User");
    }

    [Fact]
    public async Task Invoke_EmptyException()
    {
        var request = new Mock<RequestDelegate>();
        request.Setup(x => x).Throws<Exception>();

        var response = PrepareResponse(out var http);

        var middleWare = new ExceptionHandlingMiddleware(request.Object, context.Object);
        await middleWare.Invoke(http);
    }

    [Fact]
    public async Task Invoke_Exception()
    {
        var request = new Mock<RequestDelegate>();
        request.Setup(x => x).Throws(() => new Exception("Error"));

        var response = PrepareResponse(out var http);

        var middleWare = new ExceptionHandlingMiddleware(request.Object, context.Object);
        await middleWare.Invoke(http);
    }

    [Fact]
    public async Task Invoke_UserException()
    {
        var request = new Mock<RequestDelegate>();
        request.Setup(x => x).Throws(() => new UserException("خطا", "Error Details"));

        var response = PrepareResponse(out var http);

        var middleWare = new ExceptionHandlingMiddleware(request.Object, context.Object);
        await middleWare.Invoke(http);
    }

    private static Mock<HttpResponse> PrepareResponse(out HttpContext http)
    {
        var response = new Mock<HttpResponse>();
        response.Setup(x => x.StatusCode).Should().Be(HttpStatusCode.InternalServerError);
        response.Setup(x => x.ContentType).Should().Be(MediaTypeNames.Application.Json);

        var httpMock = new Mock<HttpContext>();
        httpMock.Setup(x => x.Response).Returns(response.Object);

        http = httpMock.Object;
        return response;
    }
}
