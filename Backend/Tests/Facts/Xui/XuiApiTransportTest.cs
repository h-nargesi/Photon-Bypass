using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using PhotonBypass.Domain.Servers.Entity;
using PhotonBypass.Domain.Servers.JsonType;
using PhotonBypass.Domain.Servers.Types;
using PhotonBypass.Xui.WebService;

namespace PhotonBypass.Test.Facts.Xui;

/// <summary>
/// Transport-level tests for <see cref="XuiApiService"/> against the
/// alireza0/x-ui v1.12.0/v1.14.0 contract: real paths, methods, headers, envelope
/// fields (success/msg/obj) and the two-layer inbound settings parse.
/// </summary>
public class XuiApiTransportTest
{
    [Fact]
    public async Task ListClients_LoginsThenGetsManagedInboundWithAjaxHeader()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            InboundList(
                Inbound(1, [new { id = "carrier-1", email = "carrier" }]),
                Inbound(3,
                    [new { id = "u-3", email = "User3", enable = true, totalGB = 26_843_545_600L, expiryTime = 1_760_000_000_000L, limitIp = 2 }],
                    [new { email = "User3", up = 100L, down = 200L }])));

        var clients = await BuildApi(handler).ListClients(BuildServer(inbound_id: 3, base_path: "xui84"));

        handler.Requests.Should().HaveCount(2);

        var login = handler.Requests[0];
        login.Method.Should().Be(HttpMethod.Post);
        login.Uri.AbsoluteUri.Should().Be("http://panel.local:5443/xui84/login");
        login.Body.Should().Contain("admin").And.Contain("secret");
        login.HasAjaxHeader.Should().BeFalse();

        var list = handler.Requests[1];
        list.Method.Should().Be(HttpMethod.Get);
        list.Uri.AbsoluteUri.Should().EndWith("xui/API/inbounds/");
        list.HasAjaxHeader.Should().BeTrue();
        list.Cookie.Should().Be("x-ui=s1");

        var client = clients.Should().ContainSingle().Subject;
        client.Email.Should().Be("User3");
        client.Uuid.Should().Be("u-3");
        client.Enable.Should().BeTrue();
        client.TotalTrafficBytes.Should().Be(26_843_545_600L);
        client.ExpiryTime.Should().Be(1_760_000_000_000L);
        client.LimitIp.Should().Be(2);
    }

    [Fact]
    public async Task ListClientTraffic_ReadsClientStatsOfManagedInbound()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            InboundList(
                Inbound(1, [new { id = "carrier-1", email = "carrier" }], [new { email = "carrier", up = 9L, down = 9L }]),
                Inbound(3,
                    [new { id = "u-3", email = "User3", enable = true, totalGB = 0L, expiryTime = 0L, limitIp = 0 }],
                    [new { email = "User3", up = 100L, down = 200L }])));

        var stats = await BuildApi(handler).ListClientTraffic(BuildServer(inbound_id: 3));

        var traffic = stats.Should().ContainSingle().Subject;
        traffic.Email.Should().Be("User3");
        traffic.Up.Should().Be(100);
        traffic.Down.Should().Be(200);
        traffic.Total.Should().Be(300);
    }

    [Fact]
    public async Task ListClients_WithoutConfiguredInbound_ThrowsBeforeAnyRequest()
    {
        var handler = new ScriptedHandler();

        var action = () => BuildApi(handler).ListClients(BuildServer(inbound_id: null));

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*inbound id is not configured*");
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ListClients_ConfiguredInboundMissing_ThrowsWithAvailableIds()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            InboundList(Inbound(1), Inbound(2)));

        var action = () => BuildApi(handler).ListClients(BuildServer(inbound_id: 3));

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*available inbound ids: [1, 2]*");
    }

    [Fact]
    public async Task FailedEnvelope_ThrowsWithPanelMessage()
    {
        var handler = ScriptedHandlerWith(LoginOk("s1"), Failed("inbound not found"));

        var action = () => BuildApi(handler).ListClients(BuildServer());

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*inbound not found*");
    }

    [Fact]
    public async Task FailedLogin_ThrowsWithPanelMessage()
    {
        var handler = ScriptedHandlerWith(Failed("wrong credentials"));

        var action = () => BuildApi(handler).ListClients(BuildServer());

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*login failed*")
            .WithMessage("*wrong credentials*");
    }

    [Fact]
    public async Task SessionRejected_ReloginsOnceAndRetriesTheSameRequest()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            LoginOk("s2"),
            InboundList(Inbound(1)));

        var clients = await BuildApi(handler).ListClients(BuildServer());

        clients.Should().BeEmpty();
        handler.Requests.Should().HaveCount(4);
        handler.Requests.Count(request => request.Uri.AbsoluteUri.EndsWith("/login")).Should().Be(2);
        handler.Requests[3].Cookie.Should().Be("x-ui=s2");
    }

    [Fact]
    public async Task SessionRejectedTwice_ThrowsWithoutFurtherRetries()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            new HttpResponseMessage(HttpStatusCode.Unauthorized),
            LoginOk("s2"),
            new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var action = () => BuildApi(handler).ListClients(BuildServer());

        (await action.Should().ThrowAsync<Exception>())
            .WithMessage("*rejected after a re-login*");
        handler.Requests.Should().HaveCount(4);
    }

    [Fact]
    public async Task AddClient_PostsConfiguredInboundAndContractFields()
    {
        var handler = ScriptedHandlerWith(LoginOk("s1"), Envelope("null"));

        await BuildApi(handler).AddClient(BuildServer(inbound_id: 7), new XuiClientModel
        {
            Email = "User3",
            Uuid = "u-3",
            Enable = true,
            TotalTrafficBytes = 0,
            ExpiryTime = 0,
            LimitIp = 2,
        });

        var add = handler.Requests[1];

        add.Method.Should().Be(HttpMethod.Post);
        add.Uri.AbsoluteUri.Should().EndWith("xui/API/inbounds/addClient");
        add.HasAjaxHeader.Should().BeTrue();

        var body = JsonDocument.Parse(add.Body!);

        body.RootElement.GetProperty("id").GetInt32().Should().Be(7);

        var client = JsonDocument.Parse(body.RootElement.GetProperty("settings").GetString()!)
            .RootElement.GetProperty("clients")[0];

        client.GetProperty("email").GetString().Should().Be("User3");
        client.GetProperty("id").GetString().Should().Be("u-3");
        client.GetProperty("enable").GetBoolean().Should().BeTrue();
        client.GetProperty("totalGB").GetInt64().Should().Be(0);
        client.GetProperty("expiryTime").GetInt64().Should().Be(0);
        client.GetProperty("limitIp").GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task UpdateClient_KeysPathByCurrentUuidAndCarriesTheNewOne()
    {
        var handler = ScriptedHandlerWith(LoginOk("s1"), Envelope("null"));

        await BuildApi(handler).UpdateClient(BuildServer(), "old-uuid", new XuiClientModel
        {
            Email = "User3",
            Uuid = "new-uuid",
        });

        var update = handler.Requests[1];

        update.Method.Should().Be(HttpMethod.Post);
        update.Uri.AbsoluteUri.Should().EndWith("xui/API/inbounds/updateClient/old-uuid");

        var body = JsonDocument.Parse(update.Body!);

        body.RootElement.GetProperty("id").GetInt32().Should().Be(1);

        JsonDocument.Parse(body.RootElement.GetProperty("settings").GetString()!)
            .RootElement.GetProperty("clients")[0]
            .GetProperty("id").GetString().Should().Be("new-uuid");
    }

    [Fact]
    public async Task RemoveClient_KeysPathByUuid()
    {
        var handler = ScriptedHandlerWith(LoginOk("s1"), Envelope("null"));

        await BuildApi(handler).RemoveClient(BuildServer(inbound_id: 5), "u-9");

        var remove = handler.Requests[1];

        remove.Method.Should().Be(HttpMethod.Post);
        remove.Uri.AbsoluteUri.Should().EndWith("xui/API/inbounds/5/delClient/u-9");
    }

    [Fact]
    public async Task ResetClientTraffic_KeysPathByEmail()
    {
        var handler = ScriptedHandlerWith(LoginOk("s1"), Envelope("null"));

        await BuildApi(handler).ResetClientTraffic(BuildServer(inbound_id: 5), "User3");

        var reset = handler.Requests[1];

        reset.Method.Should().Be(HttpMethod.Post);
        reset.Uri.AbsoluteUri.Should().EndWith("xui/API/inbounds/5/resetClientTraffic/User3");
    }

    [Fact]
    public async Task ListOnlineClients_ParsesEmailAndIpTimestamps()
    {
        var handler = ScriptedHandlerWith(
            LoginOk("s1"),
            Envelope("""[{"email":"User3","ips":{"1.2.3.4":1760000000123}}]"""));

        var online = await BuildApi(handler).ListOnlineClients(BuildServer());

        var entry = online.Should().ContainSingle().Subject;

        entry.Email.Should().Be("User3");
        entry.Ips.Should().ContainKey("1.2.3.4");
        entry.Ips["1.2.3.4"].Should().Be(1760000000123);
    }

    private static ServerEntity BuildServer(int? inbound_id = 1, string? base_path = null)
    {
        // JsonConfig is set directly (plaintext) instead of via the Config
        // property: parallel test classes swap ServerConfigProtection's static
        // delegates, and the protect/unprotect round-trip through the property
        // would race with them. Plaintext is passed through unchanged by
        // Unprotect regardless of the delegate state.
        var server = new ServerEntity
        {
            Id = 50,
            IsActive = true,
            RealmId = 1,
            Name = "XuiServer",
            IpAddress = "10.0.0.50",
            DomainName = "xui.photon-bypass.com",
            Features = ServerFeature.Xui | ServerFeature.V2ray,
        };

        server.JsonConfig = JsonSerializer.Serialize(new ServerConfiguration
        {
            WebApiConfig = new WebApiConfig
            {
                HostName = "panel.local",
                Port = 5443,
                Username = "admin",
                Password = "secret",
                BasePath = base_path,
                InboundId = inbound_id,
            },
        });

        return server;
    }

    private static IXuiApi BuildApi(ScriptedHandler handler)
    {
        return new XuiApiService(new SingleClientFactory(handler));
    }

    private static ScriptedHandler ScriptedHandlerWith(params HttpResponseMessage[] responses)
    {
        var handler = new ScriptedHandler();

        foreach (var response in responses)
        {
            handler.Responses.Enqueue(response);
        }

        return handler;
    }

    private static HttpResponseMessage LoginOk(string cookie)
    {
        var response = Envelope("null");
        response.Headers.Add("Set-Cookie", $"x-ui={cookie}; Path=/;");

        return response;
    }

    private static HttpResponseMessage Envelope(string obj_json)
    {
        return Json($"{{\"success\":true,\"msg\":\"\",\"obj\":{obj_json}}}");
    }

    private static HttpResponseMessage Failed(string msg)
    {
        return Json($"{{\"success\":false,\"msg\":\"{msg}\",\"obj\":null}}");
    }

    private static HttpResponseMessage Json(string body)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json"),
        };
    }

    private static HttpResponseMessage InboundList(params object[] inbounds)
    {
        return Json(JsonSerializer.Serialize(new { success = true, msg = "", obj = inbounds }));
    }

    private static object Inbound(int id, object?[]? clients = null, object[]? stats = null)
    {
        return new
        {
            id,
            settings = clients == null ? null : JsonSerializer.Serialize(new { clients }),
            clientStats = stats,
        };
    }

    private sealed class SingleClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class RecordedRequest
    {
        public required HttpMethod Method { get; init; }

        public required Uri Uri { get; init; }

        public string? Body { get; init; }

        public bool HasAjaxHeader { get; init; }

        public string? Cookie { get; init; }
    }

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly List<RecordedRequest> requests = [];

        public IReadOnlyList<RecordedRequest> Requests => requests;

        public Queue<HttpResponseMessage> Responses { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            requests.Add(new RecordedRequest
            {
                Method = request.Method,
                Uri = request.RequestUri!,
                Body = request.Content == null
                    ? null
                    : await request.Content.ReadAsStringAsync(cancellationToken),
                HasAjaxHeader = request.Headers.TryGetValues("X-Requested-With", out var ajax) &&
                                ajax.Contains("XMLHttpRequest"),
                Cookie = request.Headers.TryGetValues("Cookie", out var cookie)
                    ? string.Join("; ", cookie)
                    : null,
            });

            return Responses.Dequeue();
        }
    }
}
