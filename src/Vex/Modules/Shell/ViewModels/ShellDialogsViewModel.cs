using System.Runtime.CompilerServices;
using ReactiveUI;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 集中管理 Shell 浮层和确认框状态，避免 MainWindowViewModel 持续堆叠纯 UI 状态。
public sealed class ShellDialogsViewModel : ReactiveObject
{
    private readonly IShellStatusPublisher _statusPublisher;
    private readonly IAppLocalizer _localizer;
    private bool _isStatisticsPanelVisible;
    private bool _isPropertiesPanelVisible;
    private bool _isDeleteConfirmVisible;
    private bool _isUnsavedConfirmVisible;
    private bool _isErrorPanelVisible;
    private bool _isRenameFilePanelVisible;
    private string? _pendingDeletePath;
    private string? _pendingRenamePath;
    // 未保存确认框需要暂存用户选择后的后续动作，保存/不保存/取消会从这里恢复流程。
    private Func<Task>? _pendingUnsavedContinuation;
    private Action? _pendingUnsavedCancellation;
    private string _unsavedConfirmTitle;
    private string _unsavedConfirmMessage;
    private string _unsavedConfirmPath;
    private string _errorTitle;
    private string _errorMessage;
    private string _errorDetail;
    private string _renameFileName;

    public ShellDialogsViewModel(IShellStatusPublisher statusPublisher, IAppLocalizer localizer)
    {
        _statusPublisher = statusPublisher;
        _localizer = localizer;
        _unsavedConfirmTitle = _localizer.Get(VexL.UnsavedTitleSaveChanges);
        _unsavedConfirmMessage = _localizer.Get(VexL.UnsavedMessageBeforeContinuing);
        _unsavedConfirmPath = _localizer.Get(VexL.UnsavedDocument);
        _errorTitle = _localizer.Get(VexL.ErrorTitle);
        _errorMessage = string.Empty;
        _errorDetail = string.Empty;
        _renameFileName = string.Empty;
    }

    public bool IsStatisticsPanelVisible
    {
        get => _isStatisticsPanelVisible;
        set => SetProperty(ref _isStatisticsPanelVisible, value);
    }

    public bool IsPropertiesPanelVisible
    {
        get => _isPropertiesPanelVisible;
        set => SetProperty(ref _isPropertiesPanelVisible, value);
    }

    public bool IsDeleteConfirmVisible
    {
        get => _isDeleteConfirmVisible;
        set => SetProperty(ref _isDeleteConfirmVisible, value);
    }

    public bool IsUnsavedConfirmVisible
    {
        get => _isUnsavedConfirmVisible;
        set => SetProperty(ref _isUnsavedConfirmVisible, value);
    }

    public bool IsErrorPanelVisible
    {
        get => _isErrorPanelVisible;
        set => SetProperty(ref _isErrorPanelVisible, value);
    }

    public bool IsRenameFilePanelVisible
    {
        get => _isRenameFilePanelVisible;
        set => SetProperty(ref _isRenameFilePanelVisible, value);
    }

    public string DeleteConfirmText => _pendingDeletePath is { Length: > 0 }
        ? _localizer.Format(VexL.DeleteConfirmFileFormat, Path.GetFileName(_pendingDeletePath))
        : _localizer.Get(VexL.DeleteConfirmCurrentFile);

    public string DeleteConfirmPath => _pendingDeletePath ?? string.Empty;

    public string? PendingDeletePath => _pendingDeletePath;

    public string UnsavedConfirmTitle => _unsavedConfirmTitle;

    public string UnsavedConfirmMessage => _unsavedConfirmMessage;

    public string UnsavedConfirmPath => _unsavedConfirmPath;

    public string ErrorTitle => _errorTitle;

    public string ErrorMessage => _errorMessage;

    public string ErrorDetail => _errorDetail;

    public string RenameFileName
    {
        get => _renameFileName;
        set => SetProperty(ref _renameFileName, value);
    }

    public string RenameFilePath => _pendingRenamePath ?? string.Empty;

    public string? PendingRenamePath => _pendingRenamePath;

    public bool HasPendingUnsavedAction => _pendingUnsavedContinuation is not null;

    public void ShowStatisticsPanel()
    {
        IsStatisticsPanelVisible = true;
    }

    public void CloseStatisticsPanel()
    {
        IsStatisticsPanelVisible = false;
    }

    public void ShowPropertiesPanel()
    {
        IsPropertiesPanelVisible = true;
    }

    public void ClosePropertiesPanel()
    {
        IsPropertiesPanelVisible = false;
    }

    public void ShowDeleteConfirmation(string path)
    {
        _pendingDeletePath = path;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        OnPropertyChanged(nameof(PendingDeletePath));
        IsDeleteConfirmVisible = true;
    }

    public void ClearDeleteConfirmation()
    {
        _pendingDeletePath = null;
        OnPropertyChanged(nameof(DeleteConfirmText));
        OnPropertyChanged(nameof(DeleteConfirmPath));
        OnPropertyChanged(nameof(PendingDeletePath));
        IsDeleteConfirmVisible = false;
    }

    public void CancelDelete()
    {
        ClearDeleteConfirmation();
        _statusPublisher.PublishResource(VexL.StatusDeleteCanceled);
    }

    public void ShowRenameFilePanel(string path)
    {
        _pendingRenamePath = path;
        RenameFileName = Path.GetFileName(path);
        OnPropertyChanged(nameof(RenameFilePath));
        OnPropertyChanged(nameof(PendingRenamePath));
        IsRenameFilePanelVisible = true;
    }

    public void ClearRenameFilePanel()
    {
        _pendingRenamePath = null;
        RenameFileName = string.Empty;
        OnPropertyChanged(nameof(RenameFilePath));
        OnPropertyChanged(nameof(PendingRenamePath));
        IsRenameFilePanelVisible = false;
    }

    public void CancelRenameFile()
    {
        ClearRenameFilePanel();
        _statusPublisher.PublishResource(VexL.StatusRenameCanceled);
    }

    public void ShowUnsavedConfirmation(
        string title,
        string message,
        string path,
        Func<Task> continuation,
        Action? cancellation = null)
    {
        // 这里只保存流程闭包，不直接执行文件操作，确保确认框仍是独立的 UI 状态模块。
        _pendingUnsavedContinuation = continuation;
        _pendingUnsavedCancellation = cancellation;
        _unsavedConfirmTitle = title;
        _unsavedConfirmMessage = message;
        _unsavedConfirmPath = path;
        OnPropertyChanged(nameof(UnsavedConfirmTitle));
        OnPropertyChanged(nameof(UnsavedConfirmMessage));
        OnPropertyChanged(nameof(UnsavedConfirmPath));
        OnPropertyChanged(nameof(HasPendingUnsavedAction));
        IsUnsavedConfirmVisible = true;
        _statusPublisher.PublishResource(VexL.StatusUnsavedChangesNeedDecision);
    }

    public Func<Task>? TakePendingUnsavedContinuation()
    {
        var continuation = _pendingUnsavedContinuation;
        ClearUnsavedConfirmation();
        return continuation;
    }

    public void CancelPendingAction()
    {
        var cancellation = _pendingUnsavedCancellation;
        ClearUnsavedConfirmation();
        cancellation?.Invoke();
        _statusPublisher.PublishResource(VexL.StatusActionCanceledUnsavedKept);
    }

    public void ShowError(string messageResourceKey, Exception exception, params object?[] messageArgs)
    {
        var message = messageArgs.Length > 0
            ? _localizer.Format(messageResourceKey, messageArgs)
            : _localizer.Get(messageResourceKey);

        ShowError(_localizer.Get(VexL.ErrorTitle), message, ResolveErrorDetail(exception));
    }

    public void ShowError(string title, string message, string detail)
    {
        _errorTitle = title;
        _errorMessage = message;
        _errorDetail = detail;
        OnPropertyChanged(nameof(ErrorTitle));
        OnPropertyChanged(nameof(ErrorMessage));
        OnPropertyChanged(nameof(ErrorDetail));
        IsErrorPanelVisible = true;
        _statusPublisher.PublishResource(VexL.StatusErrorPanelShown);
    }

    public void CloseErrorPanel()
    {
        IsErrorPanelVisible = false;
        _statusPublisher.PublishResource(VexL.StatusErrorPanelClosed);
    }

    public bool CloseFloatingPanel()
    {
        if (IsUnsavedConfirmVisible)
        {
            CancelPendingAction();
            return true;
        }

        if (IsDeleteConfirmVisible)
        {
            CancelDelete();
            return true;
        }

        if (IsErrorPanelVisible)
        {
            CloseErrorPanel();
            return true;
        }

        if (IsRenameFilePanelVisible)
        {
            CancelRenameFile();
            return true;
        }

        if (IsPropertiesPanelVisible)
        {
            IsPropertiesPanelVisible = false;
            _statusPublisher.PublishResource(VexL.StatusPropertiesClosed);
            return true;
        }

        if (IsStatisticsPanelVisible)
        {
            IsStatisticsPanelVisible = false;
            _statusPublisher.PublishResource(VexL.StatusStatisticsClosed);
            return true;
        }

        return false;
    }

    private void ClearUnsavedConfirmation()
    {
        _pendingUnsavedContinuation = null;
        _pendingUnsavedCancellation = null;
        OnPropertyChanged(nameof(HasPendingUnsavedAction));
        IsUnsavedConfirmVisible = false;
    }

    private string ResolveErrorDetail(Exception exception)
    {
        return string.IsNullOrWhiteSpace(exception.Message)
            ? _localizer.Get(VexL.ErrorDetailFallback)
            : exception.Message;
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
