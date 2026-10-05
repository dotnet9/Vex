using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;

namespace Vex.Modules.Shell.Services;

/// <summary>
/// 主窗口右上角的轻量 Toast 反馈（基于 Ursa WindowToastManager），
/// 用于保存/导出/复制等完成型事件的非阻塞提示。
/// </summary>
public interface IShellToastPublisher
{
    void Publish(string message, NotificationType type = NotificationType.Success);

    /// <summary>
    /// 由主窗口在模板应用后安装承载层；未安装前发布的消息会排队等待。
    /// </summary>
    void Install(VisualLayerManager? layerManager);
}
