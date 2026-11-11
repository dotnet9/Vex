using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using Vex.Core.Models;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Services;
using Xunit;

namespace Vex.Mcp.Tests;

/// <summary>
/// MCP 传输层端到端测试：FakeDispatcher 充当工具业务，走真实 Kestrel + 官方 SDK 的
/// Streamable HTTP 传输，覆盖握手、会话、鉴权、工具与资源四个协议面。
/// </summary>
public sealed class McpServerHostTests
{
    private const string Token = "vex_mcp_test_token_0123456789abcdef";

    [Fact]
    public async Task Initialize_returns_server_info_and_session()
    {
        await using var fixture = await McpFixture.CreateAsync();
        var (status, body, session) = await fixture.PostAsync(new Dictionary<string, object?>
        {
            ["jsonrpc"] = "2.0",
            ["id"] = "init",
            ["method"] = "initialize",
            ["params"] = new Dictionary<string, object?>
            {
                ["protocolVersion"] = "2024-11-05",
                ["capabilities"] = new Dictionary<string, object?>(),
                ["clientInfo"] = new Dictionary<string, object?> { ["name"] = "Cursor", ["version"] = "1.0" },
            },
        });
        Assert.Equal(200, status);
        Assert.False(string.IsNullOrWhiteSpace(session));

        Assert.True(body.Contains("serverInfo"), $"body={body}");
        var result = JsonDocument.Parse(ExtractJson(body)).RootElement;
        Assert.Equal("vex", result.GetProperty("result").GetProperty("serverInfo").GetProperty("name").GetString());
        var protocolVersion = result.GetProperty("result").GetProperty("protocolVersion").GetString();
        Assert.False(string.IsNullOrWhiteSpace(protocolVersion));
    }

    [Fact]
    public async Task Tools_list_and_call_round_trip()
    {
        await using var fixture = await McpFixture.CreateAsync();
        var session = await fixture.InitializeAsync("Cursor");

        var list = await fixture.CallAsync("tools/list", null, session);
        var tools = list.GetProperty("result").GetProperty("tools").EnumerateArray()
            .Select(t => t.GetProperty("name").GetString()).ToList();
        Assert.Equal(["vex_echo", "vex_resource_probe"], tools);

        var call = await fixture.CallAsync("tools/call", new Dictionary<string, object?>
        {
            ["name"] = "vex_echo",
            ["arguments"] = new Dictionary<string, object?> { ["text"] = "你好 Vex" },
        }, session);
        var result = call.GetProperty("result");
        Assert.False(result.GetProperty("isError").GetBoolean());
        Assert.Contains("你好 Vex", result.GetProperty("content")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Call_tool_passes_arguments_as_json_element()
    {
        await using var fixture = await McpFixture.CreateAsync();
        var session = await fixture.InitializeAsync("Cursor");
        await fixture.CallAsync("tools/call", new Dictionary<string, object?>
        {
            ["name"] = "vex_echo",
            ["arguments"] = new Dictionary<string, object?> { ["text"] = "payload" },
        }, session);
        var raw = JsonSerializer.SerializeToElement(fixture.LastCallArguments);
        Assert.Equal("payload", raw.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Resources_list_and_read()
    {
        await using var fixture = await McpFixture.CreateAsync();
        var session = await fixture.InitializeAsync("Cursor");

        var list = await fixture.CallAsync("resources/list", null, session);
        var resources = list.GetProperty("result").GetProperty("resources").EnumerateArray()
            .Select(r => r.GetProperty("uri").GetString()).ToList();
        Assert.Equal(["vex://current-document"], resources);

        var read = await fixture.CallAsync("resources/read", new Dictionary<string, object?>
        {
            ["uri"] = "vex://current-document",
        }, session);
        var contents = read.GetProperty("result").GetProperty("contents")[0];
        Assert.Equal("text/markdown", contents.GetProperty("mimeType").GetString());
        Assert.Equal("# fake document", contents.GetProperty("text").GetString());
    }

    [Fact]
    public async Task Rejects_requests_without_valid_bearer_token()
    {
        await using var fixture = await McpFixture.CreateAsync();
        var response = await fixture.PostRawAsync(
            """{"jsonrpc":"2.0","id":1,"method":"ping"}""", token: "vex_wrong_token_000000000000");
        Assert.Equal(401, response.Status);
    }

    [Fact]
    public async Task Disabled_service_does_not_listen()
    {
        await using var fixture = await McpFixture.CreateAsync(enabled: false);
        Assert.False(fixture.Host.IsRunning);
        Assert.Equal(McpServerState.Stopped, fixture.Host.State);
    }

    /// <summary>SDK 的 Streamable HTTP 以 SSE 帧回包，提取 data 行的 JSON；纯 JSON 回包原样返回。</summary>
    private static string ExtractJson(string body)
    {
        foreach (var line in body.Split('\n'))
        {
            var trimmed = line.TrimEnd('\r');
            if (trimmed.StartsWith("data:", StringComparison.Ordinal))
            {
                return trimmed["data:".Length..].Trim();
            }
        }

        return body;
    }

    private sealed class McpFixture : IAsyncDisposable
    {
        private McpFixture(McpServerHost host, FakeDispatcher dispatcher, HttpClient http)
        {
            Host = host;
            Dispatcher = dispatcher;
            Http = http;
        }

        public McpServerHost Host { get; }

        public FakeDispatcher Dispatcher { get; }

        public HttpClient Http { get; }

        public JsonElement? LastCallArguments => Dispatcher.LastArguments;

        public string Endpoint => Http.BaseAddress!.ToString();

        public static async Task<McpFixture> CreateAsync(bool enabled = true)
        {
            var dispatcher = new FakeDispatcher();
            var settings = new AppSettings
            {
                IsMcpServerEnabled = enabled,
                McpServerHost = "127.0.0.1",
                McpServerPort = FreePort(),
                McpAuthorizationToken = Token,
            };
            var host = new McpServerHost(new FakeSettingsStore(settings), dispatcher, new FakeLocalizer());
            await host.ApplySettingsAsync();
            if (!enabled)
            {
                return new McpFixture(host, dispatcher, new HttpClient());
            }

            Assert.Equal(McpServerState.Running, host.State);
            // FakeLocalizer.Format 只回传末参，StatusText 即端点地址
            return new McpFixture(host, dispatcher, new HttpClient { BaseAddress = new Uri(host.StatusText) });
        }

        private static int FreePort()
        {
            var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            try
            {
                return ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
            }
            finally
            {
                listener.Stop();
            }
        }

        public async Task<string> InitializeAsync(string clientName)
        {
            var response = await PostAsync(new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = "init",
                ["method"] = "initialize",
                ["params"] = new Dictionary<string, object?>
                {
                    ["protocolVersion"] = "2024-11-05",
                    ["capabilities"] = new Dictionary<string, object?>(),
                    ["clientInfo"] = new Dictionary<string, object?> { ["name"] = clientName, ["version"] = "1.0" },
                },
            });
            Assert.Equal(200, response.Status);
            Assert.NotNull(response.Session);
            return response.Session!;
        }

        public async Task<JsonElement> CallAsync(string method, Dictionary<string, object?>? parameters, string session)
        {
            var payload = new Dictionary<string, object?>
            {
                ["jsonrpc"] = "2.0",
                ["id"] = Guid.NewGuid().ToString("N"),
                ["method"] = method,
            };
            if (parameters != null) payload["params"] = parameters;
            var response = await PostAsync(payload, session);
            Assert.Equal(200, response.Status);
            return JsonDocument.Parse(ExtractJson(response.Body)).RootElement.Clone();
        }

        public async Task<(int Status, string Body, string? Session)> PostAsync(
            Dictionary<string, object?> payload, string? session = null)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", Token);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            if (session != null) request.Headers.TryAddWithoutValidation("Mcp-Session-Id", session);
            return await SendAsync(request);
        }

        public async Task<(int Status, string Body, string? Session)> PostRawAsync(string json, string token)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");
            return await SendAsync(request);
        }

        private async Task<(int Status, string Body, string? Session)> SendAsync(HttpRequestMessage request)
        {
            using var response = await Http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            var session = response.Headers.TryGetValues("Mcp-Session-Id", out var values)
                ? values.FirstOrDefault()
                : null;
            return ((int)response.StatusCode, body, session);
        }

        public ValueTask DisposeAsync()
        {
            Http.Dispose();
            return new ValueTask(Host.StopAsync());
        }
    }

    private sealed class FakeSettingsStore(AppSettings settings) : IAppSettingsStore
    {
        public AppSettings Current => settings;

        public AppSettings Update(Func<AppSettings, AppSettings> update) => settings = update(settings);
    }

    private sealed class FakeLocalizer : IAppLocalizer
    {
        public event EventHandler<EventArgs>? CultureChanged
        {
            add { }
            remove { }
        }

        public System.Globalization.CultureInfo Culture => System.Globalization.CultureInfo.InvariantCulture;

        public void SetCulture(string cultureName)
        {
        }

        public string Get(string key) => key;

        public string Format(string key, params object?[] args) => args.Length > 0 ? args[^1]?.ToString() ?? string.Empty : key;
    }

    private sealed class FakeDispatcher : IMcpToolDispatcher
    {
        public JsonElement? LastArguments { get; private set; }

        public ListToolsResult ListTools() => new()
        {
            Tools =
            [
                new Tool
                {
                    Name = "vex_echo",
                    Description = "echo probe",
                    InputSchema = JsonDocument.Parse("""{"type":"object","properties":{"text":{"type":"string"}}}""").RootElement.Clone(),
                },
                new Tool { Name = "vex_resource_probe", Description = "resource probe" },
            ],
        };

        public Task<ListResourcesResult> ListResourcesAsync() => Task.FromResult(new ListResourcesResult
        {
            Resources = [new Resource { Uri = "vex://current-document", Name = "fake.md", MimeType = "text/markdown" }],
        });

        public ReadResourceResult ReadResource(string uri) => new()
        {
            Contents = [new TextResourceContents { Uri = uri, MimeType = "text/markdown", Text = "# fake document" }],
        };

        public Task<CallToolResult> CallToolAsync(string name, JsonElement? arguments)
        {
            LastArguments = arguments;
            var text = arguments is { } value && value.TryGetProperty("text", out var echo) ? echo.GetString() : "ok";
            return Task.FromResult(new CallToolResult
            {
                Content = [new TextContentBlock { Text = $"echo:{text}" }],
                IsError = false,
            });
        }
    }
}
