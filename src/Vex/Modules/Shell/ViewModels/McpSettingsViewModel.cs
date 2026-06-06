using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class McpSettingsViewModel : ReactiveObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpServerHost _serverHost;
    private bool _isEnabled;
    private string _host = "127.0.0.1";
    private int _port = 17891;
    private string _token = string.Empty;
    private string _accessScope = McpAccessScope.CurrentDocument;
    private string _allowedWorkspacePath = string.Empty;
    private bool _requireConfirmation = true;
    private string _statusText = string.Empty;

    public McpSettingsViewModel(IAppSettingsStore settingsStore, IMcpServerHost serverHost)
    {
        _settingsStore = settingsStore;
        _serverHost = serverHost;
        Load();
    }

    public event EventHandler? CloseRequested;

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
        await _serverHost.ApplySettingsAsync();
        RefreshStatus();
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    public void Cancel()
    {
        CloseRequested?.Invoke(this, EventArgs.Empty);
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
            ? $"运行中：{Endpoint}"
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
