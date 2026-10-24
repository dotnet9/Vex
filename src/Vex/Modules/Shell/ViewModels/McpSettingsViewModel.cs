using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;
using System.Reactive.Linq;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Mcp.Models;
using Vex.Modules.Mcp.Services;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>MCP 服务配置：对应原型偏好页 MCP 三个页签的数据与动作（服务、客户端、审计）。</summary>
public sealed class McpSettingsViewModel : ReactiveObject, Irihi.Avalonia.Shared.Contracts.IDialogContext
{
    private const int MinimumPort = 1024;
    private const int MaximumPort = 65535;
    private const int MinimumTokenLength = 24;
    private const int PreviewCharacterLimit = 700;
    private static readonly string BridgeAppendText = "\n\n通过 MCP 追加的段落。\n";

    private readonly IAppSettingsStore _settingsStore;
    private readonly IMcpServerHost _serverHost;
    private readonly IMcpOperationConfirmationService _confirmationService;
    private readonly IMcpToolDispatcher _toolDispatcher;
    private readonly IAppLocalizer _localizer;
    private IDisposable? _autoApplySubscription;
    private bool _isApplying;
    private bool _isEnabled;
    private string _host = "127.0.0.1";
    private string _portText = "17891";
    private int _port = 17891;
    private string _token = string.Empty;
    private string _accessScope = McpAccessScope.CurrentDocument;
    private string _allowedWorkspacePath = string.Empty;
    private bool _requireConfirmation = true;
    private bool _isTokenRevealed;
    private string _statusText = string.Empty;
    private string _validationError = string.Empty;
    private string _testResult = string.Empty;
    private string _requestMessage = string.Empty;
    private string _requestCode = string.Empty;

    public McpSettingsViewModel(
        IAppSettingsStore settingsStore,
        IMcpServerHost serverHost,
        IMcpOperationConfirmationService confirmationService,
        IMcpToolDispatcher toolDispatcher,
        IAppLocalizer localizer)
    {
        _settingsStore = settingsStore;
        _serverHost = serverHost;
        _confirmationService = confirmationService;
        _toolDispatcher = toolDispatcher;
        _localizer = localizer;
        _localizer.CultureChanged += (_, _) =>
        {
            RefreshStatus();
        };
        _serverHost.StatusChanged += (_, _) => Dispatcher.UIThread.Post(RefreshStatus);
        Load();
    }

    // 浮层关闭语义：Ursa 对话框要求 VM 实现 IDialogContext。
    public event EventHandler<object?>? RequestClose;

    public void Close() => RequestClose?.Invoke(this, null);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            this.RaiseAndSetIfChanged(ref _isEnabled, value);
            RefreshStatus();
        }
    }

    public string Host
    {
        get => _host;
        set => this.RaiseAndSetIfChanged(ref _host, value);
    }

    /// <summary>端口输入框的原始文本：原型用普通 number 输入 + 校验文案，而不是步进器。</summary>
    public string PortText
    {
        get => _portText;
        set
        {
            if (_portText == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _portText, value);
            if (TryParsePort(value, out var port))
            {
                _port = port;
                this.RaisePropertyChanged(nameof(Port));
                this.RaisePropertyChanged(nameof(Endpoint));
            }
        }
    }

    public int Port => _port;

    public string Token
    {
        get => _token;
        set
        {
            this.RaiseAndSetIfChanged(ref _token, value);
            this.RaisePropertyChanged(nameof(StdioConfiguration));
        }
    }

    public string AccessScope
    {
        get => _accessScope;
        set
        {
            if (_accessScope == value)
            {
                return;
            }

            this.RaiseAndSetIfChanged(ref _accessScope, value);
            this.RaisePropertyChanged(nameof(IsCustomFolderScope));
            this.RaisePropertyChanged(nameof(AccessScopeIndex));
        }
    }

    /// <summary>授权范围下拉框选中项：0 当前文档 / 1 当前文件夹 / 2 指定目录（对应原型 select）。</summary>
    public int AccessScopeIndex
    {
        get => AccessScope switch
        {
            McpAccessScope.CurrentFolder => 1,
            McpAccessScope.CustomFolder => 2,
            _ => 0
        };
        set
        {
            AccessScope = value switch
            {
                1 => McpAccessScope.CurrentFolder,
                2 => McpAccessScope.CustomFolder,
                _ => McpAccessScope.CurrentDocument
            };
        }
    }

    public bool IsCustomFolderScope => AccessScope == McpAccessScope.CustomFolder;

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

    public bool IsTokenRevealed
    {
        get => _isTokenRevealed;
        set => this.RaiseAndSetIfChanged(ref _isTokenRevealed, value);
    }

    public string StatusText
    {
        get => _statusText;
        private set => this.RaiseAndSetIfChanged(ref _statusText, value);
    }

    public bool IsStatusRunning { get; private set; }

    public bool IsStatusStarting { get; private set; }

    public bool IsStatusError { get; private set; }

    /// <summary>校验错误条（对应原型 #vex-mcp-error）：为空时整行隐藏。</summary>
    public string ValidationError
    {
        get => _validationError;
        private set
        {
            this.RaiseAndSetIfChanged(ref _validationError, value);
            this.RaisePropertyChanged(nameof(HasValidationError));
        }
    }

    public bool HasValidationError => !string.IsNullOrEmpty(ValidationError);

    /// <summary>测试连接结果（对应原型 #vex-test-result）。</summary>
    public string TestResult
    {
        get => _testResult;
        private set
        {
            this.RaiseAndSetIfChanged(ref _testResult, value);
            this.RaisePropertyChanged(nameof(HasTestResult));
        }
    }

    public bool HasTestResult => !string.IsNullOrEmpty(TestResult);

    /// <summary>stdio 页请求区的提示文本：原型对「请先启动服务」用红色校验条，对动作结果用正文，两者共用一个位置。</summary>
    public string RequestMessage
    {
        get => _requestMessage;
        private set
        {
            this.RaiseAndSetIfChanged(ref _requestMessage, value);
            this.RaisePropertyChanged(nameof(HasRequestError));
            this.RaisePropertyChanged(nameof(HasRequestNote));
        }
    }

    private bool _requestIsError;

    /// <summary>置请求提示；isError 决定它按红色校验条还是普通正文渲染。</summary>
    private void SetRequestMessage(string text, bool isError)
    {
        _requestIsError = isError;
        RequestMessage = text;
    }

    public bool HasRequestError => _requestIsError && !string.IsNullOrEmpty(RequestMessage);

    public bool HasRequestNote => !_requestIsError && !string.IsNullOrEmpty(RequestMessage);

    /// <summary>stdio 页请求区的文档预览（对应原型把文档正文塞进 pre.vex-code）。</summary>
    public string RequestCode
    {
        get => _requestCode;
        private set
        {
            this.RaiseAndSetIfChanged(ref _requestCode, value);
            this.RaisePropertyChanged(nameof(HasRequestCode));
        }
    }

    public bool HasRequestCode => !string.IsNullOrEmpty(RequestCode);

    public string Endpoint => $"http://{FormatEndpointHost(NormalizeHost(Host))}:{Math.Clamp(Port, MinimumPort, MaximumPort)}/mcp/";

    /// <summary>服务可用 = 本地监听已就绪；stdio 页的两个动作都先做这道门。</summary>
    public bool IsServiceReady => _serverHost.IsRunning;

    public void RegenerateToken()
    {
        Token = McpTokenGenerator.Generate();
    }

    /// <summary>眼睛按钮：在掩码与明文之间切换（原型 aria-pressed 的 .vex-token-actions）。</summary>
    public void ToggleTokenReveal()
    {
        IsTokenRevealed = !IsTokenRevealed;
    }

    public string ClientConfiguration
    {
        get
        {
            var saved = _settingsStore.Current;
            var endpoint = $"http://{FormatEndpointHost(NormalizeHost(saved.McpServerHost))}:{Math.Clamp(saved.McpServerPort ?? 17891, MinimumPort, MaximumPort)}/mcp/";
            static string Escape(string value) => System.Text.Json.JsonEncodedText.Encode(value).ToString();
            return "{\n  \"mcpServers\": {\n    \"vex\": {\n      \"url\": \"" + Escape(endpoint) + "\",\n      \"headers\": {\n        \"Authorization\": \"" + Escape("Bearer " + (saved.McpAuthorizationToken ?? "")) + "\"\n      }\n    }\n  }\n}";
        }
    }

    public Task CopyClientConfigurationAsync() => SetClipboardTextAsync(ClientConfiguration);

    /// <summary>stdio 桥接配置：命令行客户端经 vex mcp bridge 访问本机服务。</summary>
    public string StdioConfiguration
    {
        get
        {
            var endpoint = Endpoint;
            static string Escape(string value) => System.Text.Json.JsonEncodedText.Encode(value).ToString();
            return "{\n  \"mcpServers\": {\n    \"vex\": {\n      \"command\": \"vex\",\n      \"args\": [\"mcp\", \"bridge\", \"--endpoint\", \"" + Escape(endpoint) + "\"],\n      \"env\": {\n        \"VEX_MCP_TOKEN\": \"" + Escape(Token) + "\"\n      }\n    }\n  }\n}";
        }
    }

    /// <summary>测试连接：对服务端点发一次 MCP initialize 请求，结果写进 #vex-test-result。</summary>
    public async Task TestConnectionAsync()
    {
        TestResult = _localizer.Get(VexL.McpTestConnecting);
        try
        {
            using var client = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            using var request = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Post, Endpoint);
            request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + Token);
            request.Content = new System.Net.Http.StringContent(
                "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"initialize\",\"params\":{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"vex\",\"version\":\"1.0\"}}}",
                System.Text.Encoding.UTF8,
                "application/json");
            using var response = await client.SendAsync(request);
            TestResult = response.StatusCode switch
            {
                System.Net.HttpStatusCode.Unauthorized => _localizer.Get(VexL.McpTestUnauthorized),
                var code when code is >= System.Net.HttpStatusCode.BadRequest => _localizer.Format(VexL.McpTestFailedHttp, (int)code),
                _ => _localizer.Get(VexL.McpTestSuccess)
            };
        }
        catch (System.Net.Http.HttpRequestException)
        {
            TestResult = _localizer.Get(VexL.McpTestNotRunning);
        }
        catch (OperationCanceledException)
        {
            TestResult = _localizer.Get(VexL.McpTestTimeout);
        }
    }

    /// <summary>stdio「读取当前文档」：直接走 MCP 工具链，正文以代码块回显（原型截断到 700 字）。</summary>
    public async Task ReadDocumentAsync()
    {
        if (!PrepareRequestArea())
        {
            return;
        }

        var result = await _toolDispatcher.CallToolAsync("vex_get_current_document", null);
        var text = result.Content.Count > 0 ? result.Content[0].Text : string.Empty;
        if (result.IsError)
        {
            RequestCode = string.Empty;
            SetRequestMessage(text, true);
        }
        else
        {
            SetRequestMessage(string.Empty, false);
            RequestCode = text.Length > PreviewCharacterLimit ? text[..PreviewCharacterLimit] : text;
        }
    }

    /// <summary>stdio「请求修改文档」：与真实 MCP 客户端走同一条路径，
    /// 同意与否由应用的 MCP 确认对话框决定，页内只回显结果。</summary>
    public async Task RequestDocumentEditAsync()
    {
        if (!PrepareRequestArea())
        {
            return;
        }

        var arguments = System.Text.Json.JsonSerializer.SerializeToElement(new { text = BridgeAppendText });
        var result = await _toolDispatcher.CallToolAsync("vex_insert_text", arguments);
        var text = result.Content.Count > 0 ? result.Content[0].Text : string.Empty;
        RequestCode = string.Empty;
        SetRequestMessage(
            result.IsError || text.Contains("canceled", StringComparison.OrdinalIgnoreCase)
                ? _localizer.Get(VexL.McpRequestRejected)
                : _localizer.Get(VexL.McpRequestApproved),
            result.IsError);
    }

    /// <summary>原型语义：未启动服务时请求区只给一条红色提示，其余动作不再执行。</summary>
    private bool PrepareRequestArea()
    {
        RequestCode = string.Empty;
        if (!IsServiceReady)
        {
            SetRequestMessage(_localizer.Get(VexL.McpServiceRequired), true);
            return false;
        }

        SetRequestMessage(string.Empty, false);
        return true;
    }

    /// <summary>偏好页内嵌模式：改动去抖后自动落盘并重启服务（原型这一页没有「保存」按钮）。</summary>
    public void EnableAutoApply()
    {
        if (_autoApplySubscription is not null)
        {
            return;
        }

        _autoApplySubscription = this
            .WhenAnyValue(
                model => model.IsEnabled,
                model => model.PortText,
                model => model.Token,
                model => model.AccessScope,
                model => model.AllowedWorkspacePath,
                model => model.RequireConfirmation)
            .Throttle(TimeSpan.FromMilliseconds(300), RxApp.MainThreadScheduler)
            .Subscribe(changes => _ = ApplyAsync());
    }

    /// <summary>离开页面时把还在去抖窗口里的改动写掉，避免关窗丢最后一次修改。</summary>
    public Task FlushAutoApplyAsync() => _autoApplySubscription is null ? Task.CompletedTask : ApplyAsync();

    public void CancelAutoApply()
    {
        _autoApplySubscription?.Dispose();
        _autoApplySubscription = null;
    }

    /// <summary>独立对话框的「取消」：只关窗，不落盘。</summary>
    public void Cancel() => Close();

    public async Task SaveAsync()
    {
        await ApplyAsync();
        Close();
    }

    /// <summary>校验 + 落盘 + 重启监听；失败时把原因写进校验条并返回 false（不关闭对话框）。</summary>
    public async Task<bool> ApplyAsync()
    {
        if (!TryNormalize(out var normalizedToken, out var normalizedPath, out var error))
        {
            ValidationError = error;
            return false;
        }

        ValidationError = string.Empty;
        if (HasUnsavedChanges(normalizedToken, normalizedPath))
        {
            await PersistAsync(normalizedToken, normalizedPath);
        }

        return true;
    }

    private bool HasUnsavedChanges(string normalizedToken, string normalizedPath)
    {
        var saved = _settingsStore.Current;
        return saved.IsMcpServerEnabled != _isEnabled
            || (saved.McpServerPort ?? 0) != _port
            || !string.Equals(saved.McpAuthorizationToken, normalizedToken, StringComparison.Ordinal)
            || !string.Equals(saved.McpAccessScope, _accessScope, StringComparison.Ordinal)
            || !string.Equals(saved.McpAllowedWorkspacePath ?? string.Empty, normalizedPath, StringComparison.Ordinal)
            || saved.McpRequireConfirmation != _requireConfirmation;
    }

    private async Task PersistAsync(string normalizedToken, string normalizedPath)
    {
        _settingsStore.Update(settings => settings with
        {
            IsMcpServerEnabled = _isEnabled,
            McpServerHost = NormalizeHost(Host),
            McpServerPort = _port,
            McpAuthorizationToken = normalizedToken,
            McpAccessScope = _accessScope,
            McpAllowedWorkspacePath = NullIfEmpty(normalizedPath),
            McpRequireConfirmation = _requireConfirmation
        });

        _confirmationService.ResetRememberedChoices();
        this.RaisePropertyChanged(nameof(ClientConfiguration));
        _isApplying = true;
        RefreshStatus();
        try
        {
            await _serverHost.ApplySettingsAsync();
        }
        finally
        {
            _isApplying = false;
            RefreshStatus();
        }
    }

    private bool TryNormalize(out string normalizedToken, out string normalizedPath, out string error)
    {
        normalizedToken = Token.Trim();
        normalizedPath = AllowedWorkspacePath.Trim();
        if (!TryParsePort(PortText, out var port))
        {
            error = _localizer.Get(VexL.McpErrorPortRange);
            return false;
        }

        _port = port;
        PortText = port.ToString();
        if (normalizedToken.Length < MinimumTokenLength)
        {
            error = _localizer.Get(VexL.McpErrorTokenShort);
            return false;
        }

        if (_accessScope == McpAccessScope.CustomFolder && !IsAbsolutePath(normalizedPath))
        {
            error = _localizer.Get(VexL.McpErrorFolderAbsolute);
            return false;
        }

        error = string.Empty;
        return true;
    }

    private void RefreshStatus()
    {
        IsStatusStarting = _isApplying;
        IsStatusRunning = !_isApplying && _serverHost.State == McpServerState.Running;
        IsStatusError = !_isApplying && _serverHost.State == McpServerState.Faulted;
        this.RaisePropertyChanged(nameof(IsStatusStarting));
        this.RaisePropertyChanged(nameof(IsStatusRunning));
        this.RaisePropertyChanged(nameof(IsStatusError));
        this.RaisePropertyChanged(nameof(IsServiceReady));
        if (_isApplying)
        {
            StatusText = _localizer.Get(VexL.McpStatusStarting);
            return;
        }

        StatusText = _serverHost.State == McpServerState.Running
            ? _localizer.Format(VexL.McpStatusRunningFormat, Endpoint)
            : _serverHost.StatusText;
    }

    private void Load()
    {
        var settings = _settingsStore.Current;
        _isEnabled = settings.IsMcpServerEnabled ?? false;
        _host = NormalizeHost(settings.McpServerHost);
        _port = Math.Clamp(settings.McpServerPort ?? 17891, MinimumPort, MaximumPort);
        _portText = _port.ToString();
        _token = string.IsNullOrWhiteSpace(settings.McpAuthorizationToken)
            ? McpTokenGenerator.Generate()
            : settings.McpAuthorizationToken;
        _accessScope = NormalizeAccessScope(settings.McpAccessScope);
        _allowedWorkspacePath = settings.McpAllowedWorkspacePath ?? string.Empty;
        _requireConfirmation = settings.McpRequireConfirmation ?? true;
        RefreshStatus();
    }

    private static bool TryParsePort(string? text, out int port) =>
        int.TryParse(text, out port) && port >= MinimumPort && port <= MaximumPort;

    private static bool IsAbsolutePath(string path) =>
        path.Length > 0 && (Path.IsPathRooted(path) || System.Text.RegularExpressions.Regex.IsMatch(path, @"^[a-z]:[\\/]"));

    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static string NormalizeHost(string? host) => string.IsNullOrWhiteSpace(host) ? "127.0.0.1" : host.Trim();

    private static string FormatEndpointHost(string host) =>
        host.Contains(':', StringComparison.Ordinal) && !host.StartsWith("[", StringComparison.Ordinal)
            ? $"[{host}]"
            : host;

    private static string NormalizeAccessScope(string? scope) =>
        scope is McpAccessScope.CurrentFolder or McpAccessScope.CustomFolder ? scope : McpAccessScope.CurrentDocument;

    private static async Task SetClipboardTextAsync(string text)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime { MainWindow.Clipboard: { } clipboard })
        {
            return;
        }

        await clipboard.SetTextAsync(text);
    }
}
