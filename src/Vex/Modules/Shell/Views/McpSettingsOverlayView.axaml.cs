using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class McpSettingsOverlayView : UserControl
{
    public McpSettingsOverlayView(bool embedded) : this()
    {
        if (!embedded || Content is not Border border || border.Child is not Grid grid) return;
        border.Classes.Remove("dialog-card"); border.Width = double.NaN; border.MaxHeight = double.PositiveInfinity;
        grid.Children[0].IsVisible = false; grid.RowDefinitions[0].Height = new Avalonia.Controls.GridLength(0);
        if (grid.Children[2] is StackPanel footer) footer.Children[0].IsVisible = false;
    }

    public McpSettingsOverlayView()
    {
        InitializeComponent();
    }

    /// <summary>「…」目录选择：把用户选中的目录写回授权目录（对应原型的授权目录行）。</summary>
    public async void OnBrowseAllowedFolderClick(object? sender, RoutedEventArgs args)
    {
        if (TopLevel.GetTopLevel(this) is not { } owner) return;
        if (DataContext is not McpSettingsViewModel model) return;
        var folders = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = false });
        if (StorageProviderExtensions.TryGetLocalPath(folders.FirstOrDefault()) is { } folder)
            model.AllowedWorkspacePath = folder;
    }
}
