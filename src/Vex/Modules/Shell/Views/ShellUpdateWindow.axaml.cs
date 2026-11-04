using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

/// <summary>非模态更新窗（原型 layer-update）：与更新会话共用同一个 ViewModel，纯代码构建（同 HelpDocumentWindow），
/// 系统标题栏自带最小化/关闭。关窗只是收起——下载中的关闭被拦截为隐藏（进度转标题栏药丸），
/// 会话与已校验的安装包都保留。</summary>
public sealed class ShellUpdateWindow : Window
{
    private readonly ShellUpdateViewModel _model;
    private readonly IAppLocalizer _localizer;
    private readonly IShellToastPublisher? _toast;

    public ShellUpdateWindow(ShellUpdateViewModel model, IAppLocalizer localizer, IShellToastPublisher? toast)
    {
        _model = model;
        _localizer = localizer;
        _toast = toast;
        Title = localizer.Get(VexL.AboutCheckUpdate);
        Width = 620;
        SizeToContent = SizeToContent.Height;
        MaxHeight = 760;
        CanResize = false;
        ShowInTaskbar = true;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        Icon = new WindowIcon(Avalonia.Platform.AssetLoader.Open(new Uri("avares://Vex/Assets/logo.ico")));
        FontFamily = new FontFamily("Segoe UI, Microsoft YaHei UI, PingFang SC, Noto Sans CJK SC");
        this.Bind(BackgroundProperty, this.GetResourceObservable("VexShellBackgroundBrush"));
        Content = new ShellUpdateWindowView { DataContext = model };
        model.RequestClose += OnModelRequestClose;
        Closing += OnWindowClosing;
        Closed += OnWindowClosed;
        PropertyChanged += OnWindowPropertyChanged;
        localizer.CultureChanged += OnCultureChanged;
    }

    /// <summary>Owner 的 setter 是受保护的：由浮层服务在 Show 前传入主窗，供居中与层级跟随。</summary>
    internal void AttachOwner(Window? owner)
    {
        if (owner is not null && !ReferenceEquals(owner, this)) Owner = owner;
    }

    /// <summary>下载中关窗不终止下载（原型同款）：拦截为隐藏并 toast，进度改由标题栏药丸展示。</summary>
    private void OnWindowClosing(object? sender, WindowClosingEventArgs args)
    {
        if (!_model.IsDownloading) return;
        args.Cancel = true;
        _model.IsWindowPresented = false;
        Hide();
        _toast?.Publish(_localizer.Get(VexL.UpdateBackgroundToast));
    }

    private void OnModelRequestClose(object? sender, object? args) => Close();

    /// <summary>窗口可见性同步给会话：最小化/隐藏等同「不在现场」，下载完成的 toast 才会发；
    /// 最小化下载中按原型提示一次「进度转标题栏药丸」。</summary>
    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs args)
    {
        if (args.Property != WindowStateProperty) return;
        _model.IsWindowPresented = IsVisible && WindowState != WindowState.Minimized;
        if (WindowState == WindowState.Minimized && _model.IsDownloading)
            _toast?.Publish(_localizer.Get(VexL.UpdateMinimizedToast));
    }

    protected override void OnOpened(EventArgs args)
    {
        base.OnOpened(args);
        _model.IsWindowPresented = true;
    }

    private void OnWindowClosed(object? sender, EventArgs args)
    {
        _model.IsWindowPresented = false;
        _model.RequestClose -= OnModelRequestClose;
        _localizer.CultureChanged -= OnCultureChanged;
    }

    private void OnCultureChanged(object? sender, EventArgs args) => Title = _localizer.Get(VexL.AboutCheckUpdate);
}
