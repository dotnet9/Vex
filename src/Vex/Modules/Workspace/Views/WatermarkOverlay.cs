using Avalonia;
using Avalonia.Controls;

namespace Vex.Modules.Workspace.Views;

/// <summary>正文水印的产品默认值和主题绑定，绘制由基础控件负责。</summary>
public sealed class WatermarkOverlay : CodeWF.Avalonia.Controls.Controls.WatermarkOverlay
{
    public WatermarkOverlay()
    {
        Text = "维刻 Vex";
        this.Bind(InkProperty, this.GetResourceObservable("VexAccentBrush"));
    }
}
