using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Vex.Core.Markup;

/// <summary>菜单脱离视觉树时仍订阅应用资源，重新打开即可使用最新的密度。</summary>
public sealed class ApplicationResourceExtension(string key) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) =>
        Application.Current!.GetResourceObservable(key).ToBinding();
}
