using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Avalonia.Input.Platform;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class McpSettingsViewModel : ReactiveObject, Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpServerHost _serverHost;
    private readonly IMcpOperationConfirmationService _confirmationService;
    private readonly IAppLocalizer _localizer;
    private bool _isEnabled;
    private string _host = "127.0.0.1";
    private int _port = 17891;
    private string _token = string.Empty;
    private string _accessScope = McpAccessScope.CurrentDocument;
    private string _allowedWorkspacePath = string.Empty;
    private bool _requireConfirmation = true;
    private string _statusText = string.Empty;

    public McpSettingsViewModel(
        IAppSettingsStore settingsStore,
        IMcpServerHost serverHost,
        IMcpOperationConfirmationService confirmationService,
        IAppLocalizer localizer)
    {
        _settingsStore = settingsStore;
        _serverHost = serverHost;
        _confirmationService = confirmationService;
        _localizer = localizer;
        _localizer.CultureChanged += (_, _) => RefreshStatus();
        _serverHost.StatusChanged += (_, _) => Dispatcher.UIThread.Post(RefreshStatus);
        Load();
    }

    // 浮层关闭语义：Ursa 对话框要求 VM 实现 IDialogContext。
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    public bool IsEnabled
    {
        get => _isEnabled;
        set => this.RaiseAndSetIfChanged(ref _isEnabled, value);
    }

    public string Host
    {
        get => _host;
        set
        {
            this.RaiseAndSetIfChanged(ref _host, value);
            this.RaisePropertyChanged(nameof(Endpoint));
        }
    }

    public int Port
    {
        get => _port;
        set
        {
            this.RaiseAndSetIfChanged(ref _port, value);
            this.RaisePropertyChanged(nameof(Endpoint));
        }
    }

    public string Token
    {
        get => _token;
        set => this.RaiseAndSetIfChanged(ref _token, value);
    }

    public string AccessScope
    {
        get => _accessScope;
        set => this.RaiseAndSetIfChanged(ref _accessScope, value);
    }

    public string AllowedWorkspacePath
    {
        get => _allowedWorkspacePath;
        set => this.RaiseAndSetIfChanged(ref _allowedWorkspacePath, value);
    }

    public bool RequireConfirmation
    {
        get => _requireConfirmation;
        set => this.RaiseAndSetIfChanged(ref _requireConfirmation, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public string Endpoint => $"http://{FormatEndpointHost(NormalizeHost(Host))}:{Math.Clamp(Port, 1, 65535)}/mcp/";

    public bool IsCurrentDocumentScope
    {
        get => AccessScope == McpAccessScope.CurrentDocument;
        set
        {
            if (value)
            {
                AccessScope = McpAccessScope.CurrentDocument;
            }
        }
    }

    public bool IsCurrentFolderScope
    {
        get => AccessScope == McpAccessScope.CurrentFolder;
        set
        {
            if (value)
            {
                AccessScope = McpAccessScope.CurrentFolder;
            }
        }
    }

    public bool IsCustomFolderScope
    {
        get => AccessScope == McpAccessScope.CustomFolder;
        set
        {
            if (value)
            {
                AccessScope = McpAccessScope.CustomFolder;
            }
        }
    }

    public void GenerateToken()
    {
        Token = McpTokenGenerator.Generate();
    }

    public async Task CopyEndpointAsync()
    {
        await SetClipboardTextAsync(Endpoint);
    }

    public async Task CopyTokenAsync()
    {
        await SetClipboardTextAsync(Token);
    }

    public string ClientConfiguration
    {
        get
        {
            var saved = _settingsStore.Current;
            var endpoint = $"http://{FormatEndpointHost(NormalizeHost(saved.McpServerHost))}:{Math.Clamp(saved.McpServerPort ?? 17891, 1, 65535)}/mcp/";
            static string Escape(string value) => System.Text.Json.JsonEncodedText.Encode(value).ToString();
            return "{\n  \"mcpServers\": {\n    \"vex\": {\n      \"url\": \"" + Escape(endpoint) + "\",\n      \"headers\": {\n        \"Authorization\": \"" + Escape("Bearer " + (saved.McpAuthorizationToken ?? "")) + "\"\n      }\n    }\n  }\n}";
        }
    }

    public Task CopyClientConfigurationAsync() => SetClipboardTextAsync(ClientConfiguration);

    /// <summary>stdio 桥接配置（对应原型 clients 页第二段）：命令行客户端经 vex mcp bridge 访问本机服务。</summary>
    public string StdioConfiguration
    {
        get
        {
            var endpoint = Endpoint;
            static string Escape(string value) => System.Text.Json.JsonEncodedText.Encode(value).ToString();
            return "{\n  \"mcpServers\": {\n    \"vex\": {\n      \"command\": \"vex\",\n      \"args\": [\"mcp\", \"bridge\", \"--endpoint\", \"" + Escape(endpoint) + "\"],\n      \"env\": {\n        \"VEX_MCP_TOKEN\": \"" + Escape(Token) + "\"\n      }\n    }\n  }\n}";
        }
    }

    /// <summary>测试连接：对服务端点发一次 MCP initialize 请求，按 HTTP 结果刷新状态。</summary>
    public async Task TestConnectionAsync()
    {
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new System.Net.Http.HttpRequestMessage(
                System.Net.Http.HttpMethod.Post, Endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
            request.Content = new System.Net.Http.StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"vex\",\"version\":\"1.0\"}}}",
                System.Text.Encoding.UTF8, "application/json");
            using var response = await client.SendAsync(request);
            StatusText = response.IsSuccessStatusCode
                ? $"连接成功（HTTP {(int)response.StatusCode}）"
                : $"服务已响应（HTTP {(int)response.StatusCode}），请核对令牌";
        }
        catch (Exception exception)
        {
            StatusText = string.IsNullOrWhiteSpace(Token)
                ? "无法连接服务：服务未启用或地址不可达。"
                : $"无法连接服务：{exception.Message}";
        }
    }

    /// <summary>stdio 桥接读写按钮：桥接客户端工具尚未随本版本提供，先反馈计划。</summary>
    public void RequestBridgeAction(string action)
    {
        StatusText = $"「{action}」需要独立桥接客户端，当前版本尚未内置。";
    }

    public async Task SaveAsync()
    {
        var normalizedHost = NormalizeHost(Host);
        var normalizedPort = Math.Clamp(Port, 1, 65535);
        var normalizedToken = string.IsNullOrWhiteSpace(Token) ? McpTokenGenerator.Generate() : Token.Trim();
        _settingsStore.Update(settings => settings with
        {
            IsMcpServerEnabled = IsEnabled,
            McpServerHost = normalizedHost,
            McpServerPort = normalizedPort,
            McpAuthorizationToken = normalizedToken,
            McpAccessScope = AccessScope,
            McpAllowedWorkspacePath = string.IsNullOrWhiteSpace(AllowedWorkspacePath) ? null : AllowedWorkspacePath.Trim(),
            McpRequireConfirmation = RequireConfirmation
        });

        Host = normalizedHost;
        Port = normalizedPort;
        Token = normalizedToken;
        _confirmationService.ResetRememberedChoices();
        await _serverHost.ApplySettingsAsync();
        this.RaisePropertyChanged(nameof(ClientConfiguration));
        RefreshStatus();
        Close();
    }

    public void Cancel()
    {
        Close();
    }

    private void Load()
    {
        var settings = _settingsStore.Current;
        _isEnabled = settings.IsMcpServerEnabled ?? false;
        _host = NormalizeHost(settings.McpServerHost);
        _port = Math.Clamp(settings.McpServerPort ?? 17891, 1, 65535);
        _token = string.IsNullOrWhiteSpace(settings.McpAuthorizationToken)
            ? McpTokenGenerator.Generate()
            : settings.McpAuthorizationToken;
        _accessScope = NormalizeAccessScope(settings.McpAccessScope);
        _allowedWorkspacePath = settings.McpAllowedWorkspacePath ?? string.Empty;
        _requireConfirmation = settings.McpRequireConfirmation ?? true;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        StatusText = _serverHost.IsRunning
            ? _localizer.Format(VexL.McpStatusRunningFormat, Endpoint)
            : _serverHost.StatusText;
    }

    private static string NormalizeHost(string? host)
    {
        return string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();
    }

    private static string FormatEndpointHost(string host)
    {
        return host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;
    }

    private static string NormalizeAccessScope(string? scope)
    {
        return scope is McpAccessScope.CurrentFolder or McpAccessScope.CustomFolder
            ? scope
            : McpAccessScope.CurrentDocument;
    }

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }
}
