using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class McpSettingsOverlayView : UserControl
{
    private readonly bool _embedded;

    public McpSettingsOverlayView(bool embedded) : this()
    {
        _embedded = embedded;
        if (!embedded || Content is not Border border || border.Child is not Grid grid) return;
        // 偏好页内嵌形态：不要对话框外壳（标题、按钮、固定宽度），改由去抖自动落盘承接「保存」语义。
        border.Classes.Remove("dialog-card"); border.Width = double.NaN; border.MaxHeight = double.PositiveInfinity;
        grid.RowDefinitions[0].Height = new Avalonia.Controls.GridLength(0);
        grid.RowDefinitions[2].Height = new Avalonia.Controls.GridLength(0);
        HeaderRow.IsVisible = false; FooterRow.IsVisible = false;
    }

    public McpSettingsViewModel? Model => DataContext as McpSettingsViewModel;

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        // 独立对话框靠「保存」按钮提交，这里要关掉内嵌形态留下的去抖自动落盘。
        if (_embedded) Model?.EnableAutoApply();
        else Model?.CancelAutoApply();
    }

    /// <summary>切走页签或关闭偏好时把还在去抖窗口里的最后一次改动写掉。</summary>
    protected override async void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        if (_embedded) await (Model?.FlushAutoApplyAsync() ?? Task.CompletedTask);
    }

    public McpSettingsOverlayView()
    {
        InitializeComponent();
    }

    /// <summary>「…」目录选择：把用户选中的目录写回授权目录（对应原型的授权目录行）。</summary>
    public async void OnBrowseAllowedFolderClick(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not { } owner) return;
        if (Model is not { } model) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (StorageProviderExtensions.TryGetLocalPath(folders.FirstOrDefault()) is { } folder)
            model.AllowedWorkspacePath = folder;
    }
}
