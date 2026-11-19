using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Controls.Primitives;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>Native flyout lifecycle for the reusable picker; dismissing it cancels uncommitted color edits.</summary>
public sealed class MarkdownSpanStyleFlyout : Flyout
{
    private MarkdownSpanStylePickerViewModel? _model;
    public MarkdownSpanStyleFlyout()
    {
        // Inherited/compiled DataContext bindings attach when the flyout content enters the tree.
        Opened += (_, _) =>
        {
            _model = (Content as Control)?.DataContext as MarkdownSpanStylePickerViewModel;
            if (_model is null) return;
            _model.Prepare(); _model.CloseRequested += Close;
        };
        Closed += (_, _) => { if (_model is not null) _model.CloseRequested -= Close; _model = null; };
    }
    private void Close(object? sender, EventArgs e) => Hide();
    protected override Control CreatePresenter()
    {
        var presenter = base.CreatePresenter();
        presenter.Classes.Add("MdSpanStyleFlyout");
        return presenter;
    }
}
