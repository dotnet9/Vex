using Avalonia.Input;
using Vex.Core.Messaging;

namespace Vex.Modules.Shell.Services;

public sealed class ShellDropTargetHandler : IShellDropTargetHandler
{
    private readonly IShellDroppedPathReader _droppedPaths;

    public ShellDropTargetHandler(IShellDroppedPathReader droppedPaths)
    {
        _droppedPaths = droppedPaths;
    }

    public DragDropEffects GetDragEffects(DragEventArgs e)
    {
        return _droppedPaths.GetFirstLocalFile(e) is null
            ? DragDropEffects.None
            : DragDropEffects.Copy;
    }

    public void PublishDroppedPath(DragEventArgs e)
    {
        var path = _droppedPaths.GetFirstLocalFile(e);
        if (path is null)
        {
            return;
        }

        // 图片文件走插入通道（复制到 assets/ 并插入 Markdown），其余仍是打开文档流程。
        if (ShellDroppedPathReader.IsSupportedImage(path))
        {
            CodeWF.EventBus.EventBus.Default.Publish(new ShellImageDroppedCommand(path));
            return;
        }

        CodeWF.EventBus.EventBus.Default.Publish(new ShellDroppedPathCommand(path));
    }
}
