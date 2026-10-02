using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Serialization;

namespace Vex.Modules.Mcp.Services;

public sealed class McpServerHost : IMcpServerHost
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpToolDispatcher _toolDispatcher;
    private readonly IAppLocalizer _localizer;
    private HttpListener? _listener;
    private CancellationTokenSource? _cts;
    private Task? _listenTask;
    private string? _statusResourceKey;
    private object?[] _statusArgs = [];

    public McpServerHost(IAppSettingsStore settingsStore, IMcpToolDispatcher toolDispatcher, IAppLocalizer localizer)
    {
        _settingsStore = settingsStore;
        _toolDispatcher = toolDispatcher;
        _localizer = localizer;
        _localizer.CultureChanged += (_, _) => RefreshLocalizedStatus();
        SetStatus(VexL.McpStatusStopped);
    }

    public bool IsRunning { get; private set; }

    public string StatusText { get; private set; } = string.Empty;

    public async Task ApplySettingsAsync()
    {
        await StopAsync();
        var settings = _settingsStore.Current;
        if (settings.IsMcpServerEnabled != true)
        {
            IsRunning = false;
            SetStatus(VexL.McpStatusStopped);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.McpAuthorizationToken))
        {
            IsRunning = false;
            SetStatus(VexL.McpStatusTokenMissing);
            return;
        }

        var host = string.IsNullOrWhiteSpace(settings.McpServerHost) ? "127.0.0.1" : settings.McpServerHost.Trim();
        var port = Math.Clamp(settings.McpServerPort ?? 17891, 1, 65535);
        if (host is not "127.0.0.1" and not "localhost" and not "::1")
        {
            IsRunning = false;
            SetStatus(VexL.McpStatusLoopbackOnly);
            return;
        }

        try
        {
            _cts = new CancellationTokenSource();
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://{FormatEndpointHost(host)}:{port}/mcp/");
            _listener.Start();
            _listenTask = Task.Run(() => ListenAsync(_cts.Token));
            IsRunning = true;
            SetStatus(VexL.McpStatusRunningFormat, $"http://{FormatEndpointHost(host)}:{port}/mcp/");
        }
        catch (Exception exception) when (exception is HttpListenerException or InvalidOperationException)
        {
            IsRunning = false;
            SetRawStatus(exception.Message);
            await StopAsync();
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_listener is not null)
        {
            try
            {
                _listener.Stop();
                _listener.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }

        if (_listenTask is not null)
        {
            try
            {
                await _listenTask;
            }
            catch (OperationCanceledException)
            {
            }
            catch (HttpListenerException)
            {
            }
        }

        _listener = null;
        _cts?.Dispose();
        _cts = null;
        _listenTask = null;
        IsRunning = false;
        SetStatus(VexL.McpStatusStopped);
    }

    private void SetStatus(string resourceKey, params object?[] args)
    {
        _statusResourceKey = resourceKey;
        _statusArgs = args;
        RefreshLocalizedStatus();
    }

    private void SetRawStatus(string text)
    {
        _statusResourceKey = null;
        _statusArgs = [];
        StatusText = text;
    }

    private void RefreshLocalizedStatus()
    {
        if (_statusResourceKey is not { Length: > 0 } resourceKey)
        {
            return;
        }

        StatusText = _statusArgs.Length == 0
            ? _localizer.Get(resourceKey)
            : _localizer.Format(resourceKey, _statusArgs);
    }

    private async Task ListenAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && _listener is { IsListening: true } listener)
        {
            HttpListenerContext context;
            try
            {
                context = await listener.GetContextAsync();
            }
            catch (Exception exception) when (exception is ObjectDisposedException or HttpListenerException or InvalidOperationException)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }

                throw;
            }

            _ = Task.Run(() => HandleContextAsync(context), cancellationToken);
        }
    }

    private async Task HandleContextAsync(HttpListenerContext context)
    {
        try
        {
            if (!context.Request.HttpMethod.Equals("POST", StringComparison.OrdinalIgnoreCase))
            {
                await WritePlainAsync(context, 405, "Method Not Allowed");
                return;
            }

            if (!IsAuthorized(context.Request))
            {
                await WritePlainAsync(context, 401, "Unauthorized");
                return;
            }

            var request = await JsonSerializer.DeserializeAsync(
                context.Request.InputStream,
                McpJsonContext.Default.JsonRpcRequest);
            if (request is null || string.IsNullOrWhiteSpace(request.Method))
            {
                await WriteJsonAsync(context, new JsonRpcResponse("2.0", request?.Id, Error: new JsonRpcError(-32600, "Invalid request")));
                return;
            }

            var response = await DispatchAsync(request);
            if (response is null)
            {
                WriteNoContent(context);
                return;
            }

            await WriteJsonAsync(context, response);
        }
        catch (Exception exception)
        {
            await WriteJsonAsync(context, new JsonRpcResponse("2.0", null, Error: new JsonRpcError(-32603, exception.Message)));
        }
    }

    private async Task<JsonRpcResponse?> DispatchAsync(JsonRpcRequest request)
    {
        var isNotification = request.Id is null;
        if (isNotification && !request.Method!.StartsWith("notifications/", StringComparison.Ordinal))
        {
            return null;
        }

        switch (request.Method)
        {
            case "initialize":
                return new JsonRpcResponse(
                    "2.0",
                    request.Id,
                    ToJsonElement(
                    new McpInitializeResult(
                        "2025-06-18",
                        new McpServerInfo("Vex", "1.0"),
                        new McpCapabilities(new McpToolsCapability(false))),
                    McpJsonContext.Default.McpInitializeResult));
            case "ping":
                return new JsonRpcResponse("2.0", request.Id, ToJsonElement(new OperationResult("ok"), McpJsonContext.Default.OperationResult));
            case "tools/list":
                return new JsonRpcResponse("2.0", request.Id, ToJsonElement(_toolDispatcher.ListTools(), McpJsonContext.Default.McpToolsListResult));
            case "tools/call":
                var call = request.Params?.Deserialize(McpJsonContext.Default.McpToolCallParams);
                if (call?.Name is not { Length: > 0 } name)
                {
                    return new JsonRpcResponse("2.0", request.Id, Error: new JsonRpcError(-32602, "Tool name is required."));
                }

                return new JsonRpcResponse(
                    "2.0",
                    request.Id,
                    ToJsonElement(await _toolDispatcher.CallToolAsync(name, call.Arguments), McpJsonContext.Default.McpToolCallResult));
            case "notifications/initialized":
                return null;
            default:
                if (isNotification)
                {
                    return null;
                }

                return new JsonRpcResponse("2.0", request.Id, Error: new JsonRpcError(-32601, $"Unknown method: {request.Method}"));
        }
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        var token = _settingsStore.Current.McpAuthorizationToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var authorization = request.Headers["Authorization"];
        return authorization?.Equals($"Bearer {token}", StringComparison.Ordinal) == true;
    }

    private static async Task WriteJsonAsync(HttpListenerContext context, JsonRpcResponse response)
    {
        context.Response.StatusCode = 200;
        context.Response.ContentType = "application/json; charset=utf-8";
        await JsonSerializer.SerializeAsync(context.Response.OutputStream, response, McpJsonContext.Default.JsonRpcResponse);
        context.Response.Close();
    }

    private static async Task WritePlainAsync(HttpListenerContext context, int statusCode, string text)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "text/plain; charset=utf-8";
        var bytes = Encoding.UTF8.GetBytes(text);
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes);
        context.Response.Close();
    }

    private static void WriteNoContent(HttpListenerContext context)
    {
        context.Response.StatusCode = 204;
        context.Response.ContentLength64 = 0;
        context.Response.Close();
    }

    private static JsonElement ToJsonElement<T>(T value, JsonTypeInfo<T> typeInfo)
    {
        return JsonSerializer.SerializeToElement(value, typeInfo);
    }

    private static string FormatEndpointHost(string host)
    {
        return host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
    }
}
