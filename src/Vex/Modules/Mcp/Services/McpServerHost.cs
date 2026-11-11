using System.Net.Sockets;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ModelContextProtocol.AspNetCore;
using ModelContextProtocol.Protocol;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using McpSdk = ModelContextProtocol.Server;

namespace Vex.Modules.Mcp.Services;

/// <summary>
/// Vex 的 MCP 传输宿主：进程内 Kestrel 承载 ModelContextProtocol.AspNetCore 的 Streamable HTTP
/// 传输（会话握手、协议版本协商由官方 SDK 处理），Bearer 鉴权在路由前拦截；
/// 工具与资源的业务实现在 <see cref="McpToolDispatcher"/>（与 Vox 的 McpWebHost 同构）。
/// </summary>
public sealed class McpServerHost : IMcpServerHost
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpToolDispatcher _toolDispatcher;
    private readonly IAppLocalizer _localizer;
    private WebApplication? _app;
    private Task? _runTask;
    private CancellationTokenSource? _cts;
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

    public McpServerState State { get; private set; } = McpServerState.Stopped;

    public bool IsRunning => State == McpServerState.Running;

    public event EventHandler? StatusChanged;

    public string StatusText { get; private set; } = string.Empty;

    public async Task ApplySettingsAsync()
    {
        await StopAsync();
        var settings = _settingsStore.Current;
        if (settings.IsMcpServerEnabled != true)
        {
            State = McpServerState.Stopped;
            SetStatus(VexL.McpStatusStopped);
            return;
        }

        if (string.IsNullOrWhiteSpace(settings.McpAuthorizationToken))
        {
            State = McpServerState.Faulted;
            SetStatus(VexL.McpStatusTokenMissing);
            return;
        }

        var host = string.IsNullOrWhiteSpace(settings.McpServerHost) ? "127.0.0.1" : settings.McpServerHost.Trim();
        var port = Math.Clamp(settings.McpServerPort ?? 17891, 1, 65535);
        if (host is not "127.0.0.1" and not "localhost" and not "::1")
        {
            State = McpServerState.Faulted;
            SetStatus(VexL.McpStatusLoopbackOnly);
            return;
        }

        var endpoint = $"http://{FormatEndpointHost(host)}:{port}/mcp/";
        try
        {
            _cts = new CancellationTokenSource();
            var builder = WebApplication.CreateBuilder();
            builder.Services
                .AddMcpServer(o =>
                {
                    o.ServerInfo = new Implementation
                    {
                        Name = "vex",
                        Title = "Vex",
                        Version = typeof(McpServerHost).Assembly.GetName().Version?.ToString(3) ?? "1.0",
                    };
                })
                .WithHttpTransport(o => o.SessionMode = ModelContextProtocol.AspNetCore.HttpServerSessionMode.Stateful)
                .WithListToolsHandler((_, _) => ValueTask.FromResult(_toolDispatcher.ListTools()))
                .WithCallToolHandler(HandleCallAsync)
                .WithListResourcesHandler(async (_, _) => await _toolDispatcher.ListResourcesAsync())
                .WithReadResourceHandler((ctx, _) =>
                    ValueTask.FromResult(_toolDispatcher.ReadResource(ctx.Params?.Uri ?? string.Empty)));

            _app = builder.Build();
            _app.Urls.Add($"http://{FormatEndpointHost(host)}:{port}");
            _app.Use(async (context, next) =>
            {
                if (context.Request.Path.StartsWithSegments("/mcp") && !IsAuthorized(context.Request))
                {
                    context.Response.StatusCode = 401;
                    context.Response.Headers.WWWAuthenticate = "Bearer";
                    await context.Response.CompleteAsync();
                    return;
                }

                await next(context);
            });
            _app.MapMcp("/mcp");

            _app.StartAsync(_cts.Token).GetAwaiter().GetResult();
            _runTask = _app.WaitForShutdownAsync(_cts.Token);
            State = McpServerState.Running;
            SetStatus(VexL.McpStatusRunningFormat, endpoint);
        }
        catch (Exception exception) when (exception is SocketException or IOException or InvalidOperationException)
        {
            // 先释放监听器再落状态：StopAsync 会把状态改回「已停止」，失败原因必须写在它之后。
            await StopAsync();
            State = McpServerState.Faulted;
            if (exception is SocketException or IOException)
            {
                SetStatus(VexL.McpStatusPortBusy);
            }
            else
            {
                SetRawStatus(exception.Message);
            }
        }
    }

    public async Task StopAsync()
    {
        _cts?.Cancel();
        if (_app is not null)
        {
            try
            {
                await _app.StopAsync();
            }
            catch
            {
                // 已停止
            }

            try
            {
                await _app.DisposeAsync();
            }
            catch
            {
                // 已释放
            }
        }

        _app = null;
        _cts?.Dispose();
        _cts = null;
        _runTask = null;
        State = McpServerState.Stopped;
        SetStatus(VexL.McpStatusStopped);
    }

    private async ValueTask<CallToolResult> HandleCallAsync(
        McpSdk.RequestContext<CallToolRequestParams> context, CancellationToken cancellationToken)
    {
        var arguments = context.Params?.Arguments;
        JsonElement? args = arguments is { Count: > 0 }
            ? JsonSerializer.SerializeToElement(arguments)
            : null;
        return await _toolDispatcher.CallToolAsync(context.Params?.Name ?? string.Empty, args);
    }

    private bool IsAuthorized(Microsoft.AspNetCore.Http.HttpRequest request)
    {
        var token = _settingsStore.Current.McpAuthorizationToken;
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        var authorization = request.Headers.Authorization.ToString();
        return authorization?.Equals($"Bearer {token}", StringComparison.Ordinal) == true;
    }

    private void SetStatus(string resourceKey, params object?[] args)
    {
        _statusResourceKey = resourceKey;
        _statusArgs = args;
        RefreshLocalizedStatus();
        StatusChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SetRawStatus(string text)
    {
        _statusResourceKey = null;
        _statusArgs = [];
        StatusText = text;
        StatusChanged?.Invoke(this, EventArgs.Empty);
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

    private static string FormatEndpointHost(string host)
    {
        return host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
    }
}
