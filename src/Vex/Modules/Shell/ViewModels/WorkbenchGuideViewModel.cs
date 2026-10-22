using Avalonia.Threading;
using CodeWF.Toolkit.EventBus;
using ReactiveUI;
using Vex.Core.Messaging;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

/// <summary>六幕引导编排：幕次流转、打字机、门控与卡片文案状态。视图只负责聚光、定位与控件锚点（<see cref="IStage"/>）。</summary>
public sealed class WorkbenchGuideViewModel : ReactiveObject
{
    /// <summary>视图侧舞台：锚点解析、滚动、菜单开合等控件操作由视图实现。</summary>
    public interface IStage
    {
        void SetSpotlight(string[] anchors);
        void ScrollAnchor(string anchor, bool top);
        /// <summary>等待预览中的 Mermaid 图表真正出图（超时 5 秒降级放行，不卡死引导）。</summary>
        void WaitForDiagrams(Action done);
        void FitMap(double scale);
        void SelectOutlineItem(string title);
        void OpenFileMenu();
        void OpenExportSubmenu();
        void CloseMenus();
    }

    /// <summary>聚光锚点语义键，由视图解析为实际控件。</summary>
    public static class Anchor
    {
        public const string Instant = "instant";
        public const string Editor = "editor";
        public const string Preview = "preview";
        public const string Map = "map";
        public const string OutlinePanel = "outline-panel";
        public const string OutlineToggle = "outline-toggle";
        public const string TypographyButton = "typography-button";
        public const string AppearanceButton = "appearance-button";
        public const string AppearanceMenu = "appearance-menu";
        public const string LayoutButton = "layout-button";
        public const string LayoutGallery = "layout-gallery";
        public const string PresentationTools = "presentation-tools";
        public const string MenuItemFile = "menu-file";
        public const string MenuTools = "menu-tools";
        public const string MenuStrip = "menu-strip";
        public static string LayoutOption(string id) => "layout-option:" + id;
    }

    public enum GuidePhase { Intro, Live, Result }

    private const string Stage0Title = "# 成都，慢慢走\n\n";
    private const string Stage0 = Stage0Title + "这个周末，去成都。把脚步放慢一点，给茶香和街巷多留些时间。\n";
    private const string Stage1 = "# 成都，慢慢走\n\n这个周末，去成都。先喝一碗盖碗茶，给茶香和街巷多留些时间。\n\n## 出发清单\n\n- [ ] 带上相机\n- [ ] 预订车票\n\n## 第一天行程\n\n### 人民公园\n\n坐在茶社里，喝一碗盖碗茶。\n\n### 宽窄巷子\n\n沿着青砖小巷，看看老院子。\n\n## 第二天行程\n\n### 熊猫基地\n\n早一点出发，去看大熊猫。\n\n## 锦里夜游\n\n等灯笼亮起来，再去锦里走走。\n\n### 旅途随手记\n\n记下今天最喜欢的一碗茶和一条巷子。\n";
    private const string Route = "\n## 成都慢游路线\n\n```mermaid\nflowchart LR\n  A[人民公园] --> B[宽窄巷子]\n  B --> C[锦里夜游]\n```\n";
    private static readonly string Stage1WithEnding = Stage1 + "\n沿着这条路线，慢慢走。";
    private static readonly string Stage2 = Stage1WithEnding + Route;
    private static readonly string RouteEdited = Stage1WithEnding + Route.Replace("B[宽窄巷子]", "B[宽窄巷子 · 喝茶漫步]");
    private static readonly string[] Baselines = [Stage0, Stage1WithEnding, Stage1WithEnding, Stage1WithEnding, RouteEdited, RouteEdited];
    private static readonly string[] TypographyCycle = ["Simple", "InkBlack", "Column", "WeChatFormat"];

    private MainWindowViewModel? _shell;
    private IStage _stage = null!;
    private GuidePhase _phase = GuidePhase.Intro;
    private bool _paused;
    private Action? _pendingContinue;
    private GuideGate? _gate;
    private int _runToken;
    /// <summary>引导会话代号：每轮开始/结束递增，视图侧的轮询定时器据此作废。</summary>
    public int RunToken => _runToken;
    private readonly List<DispatcherTimer> _timers = [];

    public GuidePhase Phase => _phase;
    public int StepIndex { get; private set; }
    public bool Paused => _paused;
    public bool GateActive => _gate is not null;
    public string[] GateAnchorKeys => _gate?.Targets ?? [];
    public string? GateScopeKey => _gate?.Scope;

    private string _cardTitle = string.Empty;
    public string CardTitle { get => _cardTitle; private set => this.RaiseAndSetIfChanged(ref _cardTitle, value); }
    private string _cardDescription = string.Empty;
    public string CardDescription { get => _cardDescription; private set => this.RaiseAndSetIfChanged(ref _cardDescription, value); }
    private bool _cardDescriptionVisible;
    public bool CardDescriptionVisible { get => _cardDescriptionVisible; private set => this.RaiseAndSetIfChanged(ref _cardDescriptionVisible, value); }
    private string _cardMessage = string.Empty;
    public string CardMessage { get => _cardMessage; private set => this.RaiseAndSetIfChanged(ref _cardMessage, value); }
    private string _nextLabel = string.Empty;
    public string NextLabel { get => _nextLabel; private set => this.RaiseAndSetIfChanged(ref _nextLabel, value); }
    private bool _nextEnabled = true;
    public bool NextEnabled { get => _nextEnabled; private set => this.RaiseAndSetIfChanged(ref _nextEnabled, value); }
    private bool _previousVisible;
    public bool PreviousVisible { get => _previousVisible; private set => this.RaiseAndSetIfChanged(ref _previousVisible, value); }
    private string _skipLabel = string.Empty;
    public string SkipLabel { get => _skipLabel; private set => this.RaiseAndSetIfChanged(ref _skipLabel, value); }
    private bool _skipVisible = true;
    public bool SkipVisible { get => _skipVisible; private set => this.RaiseAndSetIfChanged(ref _skipVisible, value); }
    private string _counterText = string.Empty;
    public string CounterText { get => _counterText; private set => this.RaiseAndSetIfChanged(ref _counterText, value); }

    private static string T(string key) => WorkbenchText.Get(key);

    public void Begin(MainWindowViewModel shell, IStage stage)
    {
        _shell = shell; _stage = stage;
        CancelTimers(); _paused = false; _pendingContinue = null; LeaveGate();
        shell.Workbench.IsGuiding = true;
        StepIndex = 0;
        shell.Workbench.GuideStep = 0;
        _phase = GuidePhase.Intro;
        CardTitle = T("GuideIntroTitle");
        CardDescription = T("GuideIntroText");
        CardDescriptionVisible = true;
        CardMessage = string.Empty;
        SkipLabel = T("GuideIntroSkip");
        SkipVisible = true;
        NextLabel = T("GuideIntroStart");
        NextEnabled = true;
        PreviousVisible = false;
        RefreshCounter();
        Spotlight([]);
    }

    public event Action? FinishRequested;

    public void Advance()
    {
        if (_shell is null) return;
        switch (_phase)
        {
            case GuidePhase.Intro: StartAct(0); break;
            case GuidePhase.Result when StepIndex >= 5: FinishRequested?.Invoke(); break;
            case GuidePhase.Result: StartAct(StepIndex + 1); break;
            case GuidePhase.Live when _gate is not null: ProxyGate(); break;
            case GuidePhase.Live when _paused: ContinueGuide(); break;
        }
    }

    public bool CanGoPrevious => StepIndex > 0 && (_paused || _gate is not null || _phase == GuidePhase.Result);

    public bool IsFinal => StepIndex >= 5;

    public void StartAct(int index)
    {
        if (_shell is null) return;
        CancelTimers(); _paused = false; _pendingContinue = null; LeaveGate();
        StepIndex = Math.Clamp(index, 0, 5);
        _shell.Workbench.GuideStep = StepIndex;
        _phase = GuidePhase.Live;
        CloseLayers();
        switch (StepIndex)
        {
            case 0:
                EnsureText(string.Empty); _shell.Workbench.SelectLayout("instant");
                SetLiveCard([Anchor.Instant]);
                break;
            case 1:
                EnsureText(Stage1WithEnding); _shell.Workbench.SelectLayout("instant");
                SetLiveCard([Anchor.OutlineToggle]);
                break;
            case 2:
                EnsureText(Stage1WithEnding); _shell.Workbench.SelectLayout("read");
                _stage.ScrollAnchor(Anchor.Preview, top: true);
                SetLiveCard([Anchor.TypographyButton]);
                break;
            case 3:
                EnsureText(Stage1WithEnding); _shell.Workbench.SelectLayout("source");
                SetLiveCard([Anchor.Editor]);
                break;
            case 4:
                EnsureText(RouteEdited); _shell.Workbench.SelectLayout("read");
                SetLiveCard([Anchor.Preview]);
                break;
            case 5:
                EnsureText(RouteEdited); _shell.Workbench.SelectLayout("read");
                SetLiveCard([Anchor.MenuItemFile]);
                break;
        }
        Later(260, RunAct);
    }

    private void RunAct()
    {
        if (_shell is null || _phase != GuidePhase.Live) return;
        switch (StepIndex)
        {
            case 0: RunAct1(); break;
            case 1: RunAct2(); break;
            case 2: RunAct3(); break;
            case 3: RunAct4(); break;
            case 4: RunAct5(); break;
            case 5: RunAct6(); break;
        }
    }

    /// <summary>第 1 幕 · 即时与源码。</summary>
    private void RunAct1()
    {
        TypeText(Stage0Title, 28, () =>
            Pause(T("GuideAct1Step1"), [Anchor.Instant], () =>
                TypeText(Stage0, 28, () =>
                    Pause(T("GuideAct1Step2"), [Anchor.Instant], () =>
                        LayoutGate("source", "GuideGateLayoutSource", () =>
                            Later(350, () =>
                                TransitionText(Stage0, Stage1, 10, () =>
                                    Pause(T("GuideAct1Step3"), [Anchor.Editor], () =>
                                        LayoutGate("instant", "GuideGateLayoutInstant", () =>
                                            TypeText(Stage1WithEnding, 24, FinishAct))))))))));
    }

    /// <summary>第 2 幕 · 大纲与导图。</summary>
    private void RunAct2()
    {
        EnterGate(T("GuideGateOutline"), [Anchor.OutlineToggle], null, string.Empty,
            () => _shell!.Layout.ShowOutlineSidebar, () => _shell!.Layout.ShowOutline(), () =>
                Later(280, () =>
                    Pause(T("GuideAct2Step1"), [Anchor.OutlinePanel], () =>
                    {
                        _stage.SelectOutlineItem("第一天行程");
                        Later(420, () =>
                            Pause(T("GuideAct2Step2"), [Anchor.Instant], () =>
                                LayoutGate("map", "GuideGateLayoutMap", () =>
                                    Later(450, () =>
                                    {
                                        _stage.FitMap(.65);
                                        Cue(T("GuideAct2Step3"), [Anchor.Map]);
                                        Later(1500, () =>
                                            Pause(T("GuideAct2Step4"), [Anchor.Map], FinishAct));
                                    }))));
                    })));
    }

    /// <summary>第 3 幕 · 正文排版。</summary>
    private void RunAct3()
    {
        EnterGate(T("GuideGateTypography"), [Anchor.TypographyButton], null, string.Empty,
            () => _shell!.Workbench.ShowTypography, () => _shell!.Workbench.ShowTypography = true, () =>
                Later(300, () => CycleTypography(0, RunAppearanceStep)));
    }

    /// <summary>第 3 幕收尾：真实展开「外观」弹出层，提示密度与配色入口后收起。</summary>
    private void RunAppearanceStep()
    {
        EnterGate(T("GuideGateAppearance"), [Anchor.AppearanceButton], null, string.Empty,
            () => _shell!.Workbench.IsAppearanceMenuOpen, () => _shell!.Workbench.RequestAppearanceMenu(), () =>
                Later(360, () => Pause(T("GuideAct3Appearance"), [Anchor.AppearanceMenu], () =>
                {
                    if (_shell!.Workbench.IsAppearanceMenuOpen) _shell.Workbench.RequestAppearanceMenu();
                    Later(260, FinishAct);
                })));
    }

    private void CycleTypography(int index, Action done)
    {
        if (_shell is null) return;
        if (index >= TypographyCycle.Length)
        {
            _shell.Workbench.ShowTypography = false;
            Later(300, done);
            return;
        }
        var option = _shell.Appearance.TypographyOptions.FirstOrDefault(item => item.Key == TypographyCycle[index]);
        if (option is null) { CycleTypography(index + 1, done); return; }
        _shell.Appearance.SelectedTypography = option;
        var name = CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource("Vex.VexL.Typography" + option.Key);
        if (string.IsNullOrEmpty(name)) name = option.Name;
        Cue($"{T("Typography")}：{name}", [Anchor.TypographyButton]);
        Later(1200, () => CycleTypography(index + 1, done));
    }

    /// <summary>第 4 幕 · Mermaid 图表。</summary>
    private void RunAct4()
    {
        Pause(T("GuideAct4Step1"), [Anchor.Editor], () =>
            TypeText(Stage1WithEnding + "\n## 成都慢游路线\n\n", 8, () =>
            {
                PublishText(Stage2);
                _shell!.Workbench.SelectLayout("source-render");
                // 等图表真正出图再继续，避免高亮落在还没渲染的空白预览上。
                _stage.WaitForDiagrams(() =>
                {
                    _stage.ScrollAnchor(Anchor.Preview, top: false);
                    Pause(T("GuideAct4Step2"), [Anchor.Preview], () =>
                    {
                        Cue(T("GuideAct4Step3"), [Anchor.Editor]);
                        TransitionText(Stage2, RouteEdited, 20, () =>
                            Pause(T("GuideAct4Step3"), [Anchor.Preview], FinishAct));
                    });
                });
            }));
    }

    /// <summary>第 5 幕 · 演示模式：开演用卡片确认，行进中只用轻提示不打断（对应原型第 5 幕）。</summary>
    private void RunAct5()
    {
        Pause(T("GuideAct5Step1"), [Anchor.Preview], () =>
        {
            _shell!.Workbench.StartPresentation();
            Later(420, () =>
            {
                Cue(T("GuideAct5Step2"), [Anchor.Preview, Anchor.PresentationTools]);
                Later(1900, () =>
                {
                    _shell.Workbench.NextPresentation();
                    Cue(T("GuideAct5Step3"), [Anchor.PresentationTools]);
                    Later(1900, () =>
                    {
                        _shell.Workbench.ShowPresentationMap();
                        Cue(T("GuideAct5Summary"), [Anchor.PresentationTools]);
                        Later(1900, () =>
                        {
                            _shell.Workbench.StopPresentation();
                            Later(420, FinishAct);
                        });
                    });
                });
            });
        });
    }

    /// <summary>第 6 幕 · 保存与分享。</summary>
    private void RunAct6()
    {
        Pause(T("GuideAct6Step1"), [Anchor.MenuItemFile], () =>
        {
            _stage.OpenFileMenu();
            Later(300, () =>
                Pause(T("GuideAct6Step1"), [Anchor.MenuItemFile, Anchor.MenuStrip], () =>
                {
                    _stage.OpenExportSubmenu();
                    Later(300, () =>
                        Pause(T("GuideAct6Step2"), [Anchor.MenuStrip], () =>
                        {
                            _stage.CloseMenus();
                            Later(200, () =>
                                Pause(T("GuideAct6Step3"), [Anchor.MenuTools, Anchor.MenuStrip], () =>
                                {
                                    _stage.CloseMenus();
                                    Pause(T("GuideAct6Summary"), [Anchor.Preview], FinishAct);
                                }));
                        }));
                }));
        });
    }

    /// <summary>暂停段：卡片消息 +【继续】。</summary>
    private void Pause(string message, string[] anchors, Action next)
    {
        CancelTimers();
        _paused = true; _pendingContinue = next; LeaveGate();
        SetCardMessage(message, interactive: true, anchors);
    }

    private void ContinueGuide()
    {
        if (_pendingContinue is not { } next) return;
        _paused = false; _pendingContinue = null;
        SetRunningCard();
        next();
    }

    /// <summary>门：高亮目标等待；用户点到目标（谓词成立）或「继续」代点后进入下一段。</summary>
    private void EnterGate(string message, string[] targets, string? scope, string expected, Func<bool> predicate, Action proxy, Action done)
    {
        CancelTimers();
        _paused = false; _pendingContinue = null;
        var gate = new GuideGate { Targets = targets, Scope = scope, Expected = expected, Predicate = predicate, Proxy = proxy, Done = done };
        var token = _runToken;
        gate.Poll = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        gate.Poll.Tick += (_, _) =>
        {
            if (token != _runToken || !ReferenceEquals(_gate, gate)) { gate.Poll.Stop(); return; }
            if (gate.Predicate()) CompleteGate();
        };
        gate.Poll.Start();
        _gate = gate;
        SetCardMessage(message, interactive: true, targets);
    }

    private void CompleteGate()
    {
        if (_gate is not { } gate) return;
        _gate = null;
        gate.Poll.Stop();
        var done = gate.Done;
        Later(120, done);
    }

    private void ProxyGate()
    {
        if (_gate is null) return;
        _gate.Proxy();
        var gate = _gate;
        var token = _runToken;
        Later(600, () => { if (token == _runToken && ReferenceEquals(_gate, gate) && gate.Predicate()) CompleteGate(); });
    }

    private void LeaveGate()
    {
        if (_gate is not { } gate) return;
        _gate = null;
        gate.Poll.Stop();
    }

    /// <summary>门控纠正：视图判定点击落在两级门范围内但不是目标时调用。</summary>
    public void RejectPointer()
    {
        if (_gate is not { } gate) return;
        CardMessage = string.Format(T("GuideGateWrongFormat"), gate.Expected);
        NextLabel = T("GuideNext");
        NextEnabled = true;
    }

    /// <summary>两级布局门：先亮布局按钮开画廊，再亮目标卡片；点错卡片给纠正提示。</summary>
    private void LayoutGate(string layoutId, string messageKey, Action done)
    {
        var expected = WorkbenchLayoutOption.All.FirstOrDefault(option => option.Id == layoutId)?.Name ?? layoutId;
        EnterGate(T("GuideGateLayout"), [Anchor.LayoutButton], null, string.Empty,
            () => _shell!.Workbench.ShowLayouts, () => _shell!.Workbench.ShowLayouts = true, () =>
                Later(260, () =>
                    EnterGate(T(messageKey), [Anchor.LayoutOption(layoutId)], Anchor.LayoutGallery, expected,
                        () => _shell!.Workbench.WorkspaceLayout == layoutId, () => _shell!.Workbench.SelectLayout(layoutId), () =>
                            Later(260, () => { _shell!.Workbench.ShowLayouts = false; done(); }))));
    }

    /// <summary>轻提示：不打断的气泡。</summary>
    private void Cue(string message, string[] anchors)
    {
        _paused = false; _pendingContinue = null;
        Spotlight(anchors);
        CardMessage = message;
        NextLabel = T("GuideRunning");
        NextEnabled = false;
    }

    private void SetLiveCard(string[] anchors)
    {
        CardTitle = T("GuideTitle" + (StepIndex + 1));
        CardDescription = string.Empty;
        CardDescriptionVisible = false;
        Spotlight(anchors);
        SetRunningCard();
    }

    private void SetRunningCard()
    {
        CardMessage = string.Empty;
        SkipLabel = T("GuideSkip");
        SkipVisible = true;
        PreviousVisible = StepIndex > 0;
        NextLabel = T("GuideRunning");
        NextEnabled = false;
        RefreshCounter();
    }

    private void SetCardMessage(string message, bool interactive, string[] anchors)
    {
        Spotlight(anchors);
        CardMessage = message;
        SkipLabel = T("GuideSkip");
        SkipVisible = true;
        PreviousVisible = StepIndex > 0;
        NextLabel = interactive ? T("GuideNext") : T("GuideRunning");
        NextEnabled = interactive;
        RefreshCounter();
    }

    private void FinishAct()
    {
        if (_shell is null) return;
        CancelTimers(); _paused = false; _pendingContinue = null; LeaveGate();
        CloseLayers();
        _phase = GuidePhase.Result;
        CardTitle = T("GuideTitle" + (StepIndex + 1));
        CardDescription = T("GuideAct" + (StepIndex + 1) + "Summary");
        CardDescriptionVisible = true;
        CardMessage = string.Empty;
        SkipVisible = false;
        PreviousVisible = StepIndex > 0;
        NextLabel = StepIndex >= 5 ? T("GuideStart") : T("GuideNext");
        NextEnabled = true;
        RefreshCounter();
    }

    public void SkipAct()
    {
        if (_shell is null || _phase != GuidePhase.Live) return;
        CancelTimers(); _paused = false; _pendingContinue = null; LeaveGate();
        PublishText(Baselines[StepIndex]);
        CloseLayers();
        if (StepIndex >= 5) FinishAct();
        else StartAct(StepIndex + 1);
    }

    public void Stop()
    {
        CancelTimers(); _paused = false; _pendingContinue = null; LeaveGate();
        _shell = null; _stage = null!;
    }

    /// <summary>收起演示期间打开的图层；演示模式仍开启时先退出。</summary>
    private void CloseLayers()
    {
        if (_shell is null) return;
        _shell.Workbench.ShowTypography = false; _shell.Workbench.ShowMapSettings = false; _shell.Workbench.ShowLayouts = false;
        _stage.CloseMenus();
        if (_shell.Workbench.IsPresenting) _shell.Workbench.StopPresentation();
    }

    private void RefreshCounter() => CounterText = string.Format(
        CodeWF.Toolkit.Lang.I18nManager.Instance.GetResource(VexL.GuideStepFormat) ?? string.Empty, StepIndex + 1);

    private void Spotlight(string[] anchors) => _stage.SetSpotlight(anchors);

    private void PublishText(string text) =>
        EventBus.Default.Publish(new MarkdownTextChangedCommand(text, 1, 1, text.Count(character => character == '\n') + 1));

    private void EnsureText(string text)
    {
        if (_shell is not null && _shell.Markdown != text) PublishText(text);
    }

    private static int CommonPrefix(string left, string right)
    {
        var index = 0;
        while (index < left.Length && index < right.Length && left[index] == right[index]) index++;
        return index;
    }

    private void Later(int milliseconds, Action action)
    {
        if (_shell is null) return;
        var token = _runToken;
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(milliseconds) };
        timer.Tick += (_, _) =>
        {
            timer.Stop(); _timers.Remove(timer);
            if (_shell is not null && token == _runToken) action();
        };
        _timers.Add(timer);
        timer.Start();
    }

    private void CancelTimers()
    {
        _runToken++;
        foreach (var timer in _timers) timer.Stop();
        _timers.Clear();
    }

    /// <summary>打字机：从现有文本的公共前缀续打，逐字应用。</summary>
    private void TypeText(string target, int speed, Action done)
    {
        var current = _shell!.Markdown;
        var prefix = CommonPrefix(current, target);
        if (current.Length > prefix) PublishText(target[..prefix]);
        var token = _runToken;
        var index = prefix;
        void Step()
        {
            if (_shell is null || token != _runToken) return;
            if (index >= target.Length) { done(); return; }
            index++;
            PublishText(target[..index]);
            Later(speed, Step);
        }
        Later(speed, Step);
    }

    /// <summary>改写过渡：保留公共前后缀，把中段逐字变化。</summary>
    private void TransitionText(string from, string to, int speed, Action done)
    {
        EnsureText(from);
        var prefix = CommonPrefix(from, to);
        var fromSuffix = from.Length; var toSuffix = to.Length;
        while (fromSuffix > prefix && toSuffix > prefix && from[fromSuffix - 1] == to[toSuffix - 1]) { fromSuffix--; toSuffix--; }
        var middle = to[prefix..toSuffix];
        var token = _runToken;
        var index = 0;
        void Step()
        {
            if (_shell is null || token != _runToken) return;
            if (index > middle.Length) { done(); return; }
            PublishText(to[..prefix] + middle[..index] + to[toSuffix..]);
            index++;
            Later(speed, Step);
        }
        Later(speed, Step);
    }

    private sealed class GuideGate
    {
        public string[] Targets = [];
        public string? Scope;
        public string Expected = string.Empty;
        public Func<bool> Predicate = () => false;
        public Action Proxy = () => { };
        public Action Done = () => { };
        public DispatcherTimer Poll = null!;
    }
}
