using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Motion;
using Vex.Core.Services;
using Vex.Controls;
using Vex.Modules.Shell.Services;

namespace Vex.Modules.Shell.ViewModels;

// 管理 Shell 窗口布局状态，避免主 ViewModel 继续混入纯窗口显示逻辑。
public sealed class ShellWindowLayoutViewModel : ReactiveObject
{
    private readonly IAppSettingsStore _settingsStore;
    private readonly IShellStatusPublisher _statusPublisher;
    private bool _isSidebarVisible = true;
    private bool _isStatusBarVisible = true;
    private bool _isPreviewVisible = true;
    private bool _isAlwaysOnTop;
    private WindowState _windowState;
    private WindowState _windowStateBeforeFullScreen;
    private bool _isPresentationMode;
    private bool _isSourceMode = true;
    private string _motionMode = MotionModes.Full;
    private bool _isFocusMode;
    private bool _isTypewriterMode;
    private bool? _sidebarVisibleBeforeFocus;
    private bool? _statusBarVisibleBeforeFocus;
    private double _sidebarWidth = DefaultSidebarWidth;
    private bool _isSidebarResizing;
    private bool _isOutlineVisible;
    private double _outlineWidth = 240;
    private double _workspaceWidth = 1280;
    private bool _outlineNarrowOverride;
    private bool? _outlineBeforeFocus;
    private bool _isOutlineResizing;
    private readonly WidthAnimator _sidebarAnimator;
    private readonly WidthAnimator _outlineAnimator;
    private readonly Dictionary<string, double> _splitRatios;

    /// <summary>侧栏默认宽度，与 design/index.html 主区栅格一致。</summary>
    public const double DefaultSidebarWidth = 226d;

    /// <summary>原型 --block-gap：块之间的间隙宽度，分栏把手本体即这段间隙。</summary>
    public const double BlockGap = 4d;

    /// <summary>原型 .pane-collapse：24px 圆钮，距块内边缘 8px。
    /// 底距不再照抄原型的 18：原生外层网格已经带 10px 块缘外边距，而原型把钮挂在 .workspace 的定位框上，
    /// 所以按 --vex-axis-col(20) 反推 底距 = 轴线 − 半径 = 8，圆心和状态胶囊、缩放坞严格落在同一根线上。</summary>
    private const double PaneCollapseSize = 24d,
        PaneCollapseInset = 8d,
        PaneCollapseBottom = VexBottomAxis.Column - PaneCollapseSize / 2;

    private const double MinSidebarWidth = 200d;

    private const double MaxSidebarWidth = 560d;

    public ShellWindowLayoutViewModel(
        IAppSettingsStore settingsStore,
        IShellStatusPublisher statusPublisher)
    {
        _sidebarAnimator = new WidthAnimator(() =>
        {
            foreach (var name in new[] { nameof(VisibleSidebarWidth), nameof(SidebarColumnWidth), nameof(MinimumVisibleSidebarWidth), nameof(MaximumVisibleSidebarWidth) })
                OnPropertyChanged(name);
        });
        _outlineAnimator = new WidthAnimator(NotifyOutlineLayout);
        _settingsStore = settingsStore;
        _statusPublisher = statusPublisher;
        var settings = _settingsStore.Current;
        _isSidebarVisible = settings.IsSidebarVisible ?? true;
        _isStatusBarVisible = settings.IsStatusBarVisible ?? true;
        _isPreviewVisible = settings.IsPreviewVisible ?? true;
        _isSourceMode = settings.IsSourceMode ?? true;
        _isAlwaysOnTop = settings.IsAlwaysOnTop ?? false;
        _motionMode = settings.MotionMode ?? MotionModes.Full;
        _isTypewriterMode = settings.IsTypewriterMode ?? false;
        _isFocusMode = settings.IsFocusMode ?? false;
        _sidebarWidth = Math.Clamp(settings.SidebarWidth ?? DefaultSidebarWidth, MinSidebarWidth, MaxSidebarWidth);
        _isOutlineVisible = settings.IsOutlineVisible ?? false;
        _outlineWidth = Math.Clamp(settings.OutlineWidth ?? 240, 180, 480);
        _splitRatios = ParseRatios(settings.ViewSplitRatios);
        MotionResources.Apply(_motionMode);
        if (_isFocusMode)
        {
            _sidebarVisibleBeforeFocus = true;
            _statusBarVisibleBeforeFocus = true;
            _isSidebarVisible = false;
            _isStatusBarVisible = false;
        }
        // 折叠钮贴在块角，块宽（含动画每帧）一变就要重算外边距，集中挂一次比在每个通知点补发更可靠。
        PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(SidebarColumnWidth):
                case nameof(ShowSidebar):
                    OnPropertyChanged(nameof(SidebarCollapseMargin));
                    OnPropertyChanged(nameof(ShowPaneCollapses));
                    break;
                case nameof(OutlineColumnWidth):
                case nameof(ShowOutlineSidebar):
                    OnPropertyChanged(nameof(OutlineCollapseMargin));
                    break;
            }
        };
    }

    public bool IsSidebarVisible
    {
        get => _isSidebarVisible;
        set
        {
            var widthBefore = VisibleSidebarWidth;
            if (!SetProperty(ref _isSidebarVisible, value)) return;
            OnPropertyChanged(nameof(ShowSidebar));
            OnPropertyChanged(nameof(SidebarColumnWidth));
            OnPropertyChanged(nameof(VisibleSidebarWidth));
            OnPropertyChanged(nameof(SidebarSplitterWidth));
            OnPropertyChanged(nameof(MinimumVisibleSidebarWidth));
            OnPropertyChanged(nameof(MaximumVisibleSidebarWidth));
            _sidebarAnimator.Animate(widthBefore, SidebarTargetWidth, MotionResources.Mid);
            NotifyOutlineLayout();
            PersistLayoutSettings();
        }
    }

    public bool IsStatusBarVisible
    {
        get => _isStatusBarVisible;
        set
        {
            if (SetProperty(ref _isStatusBarVisible, value))
            {
                OnPropertyChanged(nameof(ShowStatusBar));
                PersistLayoutSettings();
            }
        }
    }

    public bool IsPreviewVisible
    {
        get => _isPreviewVisible;
        set
        {
            if (SetProperty(ref _isPreviewVisible, value))
            {
                OnPropertyChanged(nameof(PreviewColumnWidth));
                OnPropertyChanged(nameof(PreviewSplitterWidth));
                PersistLayoutSettings();
            }
        }
    }

    /// <summary>侧栏当前宽度（px）：可拖分隔条调整并记忆。</summary>
    public double SidebarWidth
    {
        get => _sidebarWidth;
        set
        {
            var clamped = Math.Clamp(value, MinSidebarWidth, MaxSidebarWidth);
            if (!SetProperty(ref _sidebarWidth, clamped))
            {
                return;
            }

            OnPropertyChanged(nameof(SidebarColumnWidth));
            OnPropertyChanged(nameof(VisibleSidebarWidth));
            NotifyOutlineLayout();
            if (!_isSidebarResizing) PersistLayoutSettings();
        }
    }

    /// <summary>拖动期间即时更新内存布局，结束时统一保存，避免每个像素同步写入配置。</summary>
    public void BeginSidebarResize() => _isSidebarResizing = true;

    public void CompleteSidebarResize()
    {
        _isSidebarResizing = false;
        if (_settingsStore.Current.SidebarWidth != SidebarWidth) PersistLayoutSettings();
    }

    public GridLength SidebarColumnWidth
    {
        get => new(VisibleSidebarWidth);
        set
        {
            // 动画期间的插值宽度不回写用户设定宽度。
            if (ShowSidebar && _sidebarAnimator.Value is null && value.IsAbsolute && double.IsFinite(value.Value)) SidebarWidth = value.Value;
        }
    }

    public double MinimumVisibleSidebarWidth => ShowSidebar && _sidebarAnimator.Value is null ? MinSidebarWidth : 0;
    public double MaximumVisibleSidebarWidth => ShowSidebar || _sidebarAnimator.Value is not null ? MaxSidebarWidth : 0;
    public bool ShowSidebar => IsSidebarVisible && !IsPresentationMode;
    public bool ShowStatusBar => IsStatusBarVisible && !IsPresentationMode;

    /// <summary>原型 .pane-collapse 左钮：展开时落在文件块内侧下角（右缘内缩 8px），折叠时贴画布左窗边。</summary>
    public Thickness SidebarCollapseMargin => PaneCollapseMargin(ShowSidebar, VisibleSidebarWidth, false);

    /// <summary>原型 .pane-collapse 右钮：展开时落在大纲块内侧下角，折叠时贴画布右窗边。</summary>
    public Thickness OutlineCollapseMargin => PaneCollapseMargin(ShowOutlineSidebar, VisibleOutlineWidth, true);

    /// <summary>原型 focus/presentation 态：两种模式下块角折叠钮与悬浮胶囊同进退。</summary>
    public bool ShowPaneCollapses => !IsPresentationMode && !IsFocusMode;

    /// <summary>原型 .split-handle：分屏比例限 20%–80%，键盘方向键每次走 5%。</summary>
    public const double SplitRatioMin = .2, SplitRatioMax = .8, SplitRatioStep = .05;

    /// <summary>原型 vex-design-split-ratio：按「视图预设 + 轴向」记住每处分屏的比例，缺省对半。</summary>
    public double SplitRatio(string key) => _splitRatios.TryGetValue(key, out var ratio) ? ratio : .5;

    public void SetSplitRatio(string key, double value)
    {
        var clamped = Math.Clamp(value, SplitRatioMin, SplitRatioMax);
        if (_splitRatios.TryGetValue(key, out var current) && Math.Abs(current - clamped) < .0005) return;
        _splitRatios[key] = clamped;
        PersistLayoutSettings();
    }

    private static string SerializeRatios(Dictionary<string, double> ratios) => string.Join(';',
        ratios.Select(pair => pair.Key + "=" + pair.Value.ToString("0.###", CultureInfo.InvariantCulture)));

    private static Dictionary<string, double> ParseRatios(string? stored)
    {
        var parsed = new Dictionary<string, double>();
        foreach (var entry in (stored ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var split = entry.IndexOf('=');
            if (split <= 0 || !double.TryParse(entry[(split + 1)..], NumberStyles.Float, CultureInfo.InvariantCulture, out var value)) continue;
            parsed[entry[..split]] = Math.Clamp(value, SplitRatioMin, SplitRatioMax);
        }
        return parsed;
    }

    // 对齐方向决定让位落在哪一侧：左钮靠左对齐故偏移写在左边距，右钮相反；折叠时归零即贴画布窗边。
    private static Thickness PaneCollapseMargin(bool open, double columnWidth, bool right)
    {
        var inset = open ? Math.Max(0, columnWidth - PaneCollapseInset - PaneCollapseSize) : 0;
        return right ? new Thickness(0, 0, inset, PaneCollapseBottom) : new Thickness(inset, 0, 0, PaneCollapseBottom);
    }

    public bool IsOutlineVisible
    {
        get => _isOutlineVisible;
        set
        {
            var widthBefore = VisibleOutlineWidth;
            _outlineNarrowOverride = value && IsOutlineNarrow;
            SetProperty(ref _isOutlineVisible, value);
            NotifyOutlineLayout();
            _outlineAnimator.Animate(widthBefore, OutlineTargetWidth, MotionResources.Mid);
            PersistLayoutSettings();
        }
    }

    public bool ShowOutlineSidebar => IsOutlineVisible && !IsPresentationMode && !IsFocusMode
        && (!IsOutlineNarrow || _outlineNarrowOverride);
    private bool IsOutlineNarrow => _workspaceWidth - VisibleSidebarWidth - OutlineWidth < 440;
    public double WorkspaceWidth
    {
        get => _workspaceWidth;
        set
        {
            _workspaceWidth = value;
            if (!IsOutlineNarrow) _outlineNarrowOverride = false;
            NotifyOutlineLayout();
        }
    }

    public double OutlineWidth
    {
        get => _outlineWidth;
        set
        {
            if (!double.IsFinite(value) || !SetProperty(ref _outlineWidth, Math.Clamp(value, 180, 480))) return;
            NotifyOutlineLayout();
            if (!_isOutlineResizing) PersistLayoutSettings();
        }
    }
    private double VisibleOutlineWidth => _outlineAnimator.Value ?? OutlineTargetWidth;
    private double OutlineTargetWidth => ShowOutlineSidebar ? OutlineWidth : 0;
    public GridLength OutlineColumnWidth
    {
        get => new(VisibleOutlineWidth);
        // 动画期间的插值宽度不回写用户设定宽度。
        set { if (ShowOutlineSidebar && _outlineAnimator.Value is null && value.IsAbsolute) OutlineWidth = value.Value; }
    }
    public double MinimumOutlineWidth => ShowOutlineSidebar && _outlineAnimator.Value is null ? 180 : 0;
    public double MaximumOutlineWidth => ShowOutlineSidebar || _outlineAnimator.Value is not null ? 480 : 0;
    public GridLength OutlineSplitterWidth => new(ShowOutlineSidebar ? BlockGap : 0);
    public void BeginOutlineResize() => _isOutlineResizing = true;
    public void CompleteOutlineResize() { _isOutlineResizing = false; PersistLayoutSettings(); }
    public void ToggleOutline() => IsOutlineVisible = !ShowOutlineSidebar;
    private void NotifyOutlineLayout()
    {
        foreach (var name in new[] { nameof(ShowOutlineSidebar), nameof(OutlineColumnWidth), nameof(OutlineSplitterWidth), nameof(MinimumOutlineWidth), nameof(MaximumOutlineWidth) })
            OnPropertyChanged(name);
    }
    /// <summary>演示只临时隐藏界面，不改写用户布局偏好。</summary>
    public bool IsPresentationMode
    {
        get => _isPresentationMode;
        set
        {
            if (!SetProperty(ref _isPresentationMode, value)) return;
            NotifyOutlineLayout();
            foreach (var name in new[] { nameof(ShowSidebar), nameof(ShowStatusBar), nameof(SidebarColumnWidth), nameof(VisibleSidebarWidth), nameof(SidebarSplitterWidth), nameof(MinimumVisibleSidebarWidth), nameof(MaximumVisibleSidebarWidth) }) OnPropertyChanged(name);
        }
    }

    private bool _isCompactWindow;
    public bool IsCompactWindow
    {
        get => _isCompactWindow;
        set { if (!SetProperty(ref _isCompactWindow, value)) return; OnPropertyChanged(nameof(VisibleSidebarWidth)); OnPropertyChanged(nameof(SidebarColumnWidth)); }
    }
    private double SidebarTargetWidth => ShowSidebar ? IsCompactWindow && Math.Abs(SidebarWidth - DefaultSidebarWidth) < .01 ? 200 : SidebarWidth : 0;
    public double VisibleSidebarWidth => _sidebarAnimator.Value ?? SidebarTargetWidth;

    public GridLength SidebarSplitterWidth => ShowSidebar ? new GridLength(BlockGap) : new GridLength(0);

    public GridLength SourceColumnWidth => IsSourceMode ? new GridLength(1, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewColumnWidth => IsPreviewVisible ? new GridLength(1.06, GridUnitType.Star) : new GridLength(0);

    public GridLength PreviewSplitterWidth => IsSourceMode && IsPreviewVisible ? new GridLength(1) : new GridLength(0);

    /// <summary>
    /// 界面动效三档（原型 data-motion）：完整、精简（时长 ×0.35）、关闭（时长归零，过渡立即到达终态）。
    /// </summary>
    public string MotionMode
    {
        get => _motionMode;
        set
        {
            if (!SetProperty(ref _motionMode, value)) return;
            MotionResources.Apply(value);
            _statusPublisher.PublishResource(value == MotionModes.Off ? VexL.StatusMotionDisabled : VexL.StatusMotionEnabled);
            PersistLayoutSettings();
        }
    }

    /// <summary>视图菜单与偏好页共用的动效快开关：只切「开 ↔ 关闭」，关掉再打开回到完整档。</summary>
    public bool EnableMotion
    {
        get => _motionMode != MotionModes.Off;
        set => MotionMode = value ? MotionModes.Full : MotionModes.Off;
    }

    /// <summary>
    /// 专注模式：隐藏侧栏与状态栏（保留标题栏），退出时恢复进入前的可见性。
    /// </summary>
    public bool IsFocusMode
    {
        get => _isFocusMode;
        set
        {
            if (!SetProperty(ref _isFocusMode, value))
            {
                return;
            }

            if (value)
            {
                _outlineBeforeFocus = IsOutlineVisible;
                _sidebarVisibleBeforeFocus = IsSidebarVisible;
                _statusBarVisibleBeforeFocus = IsStatusBarVisible;
                _isSidebarVisible = false;
                _isStatusBarVisible = false;
            }
            else
            {
                _isOutlineVisible = _outlineBeforeFocus ?? true;
                _isSidebarVisible = _sidebarVisibleBeforeFocus ?? true;
                _isStatusBarVisible = _statusBarVisibleBeforeFocus ?? true;
            }

            OnPropertyChanged(nameof(IsSidebarVisible));
            OnPropertyChanged(nameof(IsOutlineVisible));
            OnPropertyChanged(nameof(ShowSidebar));
            OnPropertyChanged(nameof(ShowStatusBar));
            NotifyOutlineLayout();
            OnPropertyChanged(nameof(IsStatusBarVisible));
            OnPropertyChanged(nameof(SidebarColumnWidth));
            OnPropertyChanged(nameof(SidebarSplitterWidth));
            OnPropertyChanged(nameof(VisibleSidebarWidth));
            OnPropertyChanged(nameof(MinimumVisibleSidebarWidth));
            OnPropertyChanged(nameof(MaximumVisibleSidebarWidth));
            _statusPublisher.PublishResource(value
                ? VexL.StatusFocusModeEnabled
                : VexL.StatusFocusModeDisabled);
            PersistLayoutSettings();

            if (!value)
            {
                FocusEditor();
            }
        }
    }

    /// <summary>打字机模式：编辑器把光标行保持在视口垂直居中。</summary>
    public bool IsTypewriterMode
    {
        get => _isTypewriterMode;
        set
        {
            if (SetProperty(ref _isTypewriterMode, value))
            {
                _statusPublisher.PublishResource(value
                    ? VexL.StatusTypewriterEnabled
                    : VexL.StatusTypewriterDisabled);
                PersistLayoutSettings();
                FocusEditor();
            }
        }
    }

    public bool IsAlwaysOnTop
    {
        get => _isAlwaysOnTop;
        set
        {
            if (SetProperty(ref _isAlwaysOnTop, value))
            {
                PersistLayoutSettings();
            }
        }
    }

    public bool IsFullScreen
    {
        get => CurrentWindowState == WindowState.FullScreen;
        set => CurrentWindowState = value ? WindowState.FullScreen : _windowStateBeforeFullScreen;
    }

    public WindowState CurrentWindowState
    {
        get => _windowState;
        set
        {
            if (_windowState == value) return;
            if (_windowState != WindowState.FullScreen) _windowStateBeforeFullScreen = _windowState;
            this.RaiseAndSetIfChanged(ref _windowState, value);
            OnPropertyChanged(nameof(IsFullScreen));
        }
    }

    public bool IsSourceMode
    {
        get => _isSourceMode;
        set
        {
            if (SetProperty(ref _isSourceMode, value))
            {
                OnPropertyChanged(nameof(SourceColumnWidth));
                OnPropertyChanged(nameof(PreviewSplitterWidth));
                PersistLayoutSettings();
            }
        }
    }

    public void ToggleSidebar()
    {
        IsSidebarVisible = !IsSidebarVisible;
    }

    public void ToggleMotion()
    {
        EnableMotion = !EnableMotion;
    }

    public void ToggleFocusMode()
    {
        IsFocusMode = !IsFocusMode;
    }

    public void ToggleTypewriterMode()
    {
        IsTypewriterMode = !IsTypewriterMode;
    }

    public void ShowOutline()
    {
        IsOutlineVisible = true;
    }

    public void ShowFiles()
    {
        IsSidebarVisible = true;
        SelectSidebarTab(0);
    }

    public void TogglePreview()
    {
        IsPreviewVisible = !IsPreviewVisible;
    }

    public void ToggleStatusBar()
    {
        IsStatusBarVisible = !IsStatusBarVisible;
    }

    public void ToggleSourceMode()
    {
        // 源代码模式现在只控制源码编辑面板，不再联动侧边栏或预览面板。
        IsSourceMode = !IsSourceMode;
        _statusPublisher.PublishResource(IsSourceMode ? VexL.StatusSourceModeEnabled : VexL.StatusSourceModeDisabled);
        if (IsSourceMode)
        {
            FocusEditor();
        }
    }

    public void ToggleAlwaysOnTop()
    {
        IsAlwaysOnTop = !IsAlwaysOnTop;
    }

    public void ToggleFullScreen()
    {
        IsFullScreen = !IsFullScreen;
    }

    public void ToggleMaximize() => CurrentWindowState = CurrentWindowState == WindowState.Maximized
        ? WindowState.Normal : WindowState.Maximized;

    private void FocusEditor()
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new EditorActionCommand(EditorActionKind.FocusEditor));
    }

    private void SelectSidebarTab(int selectedIndex)
    {
        CodeWF.Toolkit.EventBus.EventBus.Default.Publish(new ShellSidebarTabSelectedCommand(selectedIndex));
    }

    private void PersistLayoutSettings()
    {
        if (IsPresentationMode) return;
        _settingsStore.Update(settings => settings with
        {
            IsSidebarVisible = IsSidebarVisible,
            IsStatusBarVisible = IsStatusBarVisible,
            IsPreviewVisible = IsPreviewVisible,
            IsSourceMode = IsSourceMode,
            IsAlwaysOnTop = IsAlwaysOnTop,
            SidebarWidth = SidebarWidth,
            IsOutlineVisible = IsOutlineVisible,
            OutlineWidth = OutlineWidth,
            MotionMode = MotionMode,
            IsFocusMode = IsFocusMode,
            IsTypewriterMode = IsTypewriterMode,
            ViewSplitRatios = SerializeRatios(_splitRatios)
        });
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
