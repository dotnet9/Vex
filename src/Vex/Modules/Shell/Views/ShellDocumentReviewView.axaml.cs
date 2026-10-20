using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Services;

namespace Vex.Modules.Shell.Views;

public partial class ShellDocumentReviewView : UserControl
{
    public ShellDocumentReviewView()
    {
        InitializeComponent();
        AttachedToVisualTree += (_, _) => Dispatcher.UIThread.Post(() =>
        {
            if (TopLevel.GetTopLevel(this) is { } top) { ReviewCard.MaxWidth = top.Bounds.Width - 48; ReviewCard.MaxHeight = top.Bounds.Height - 100; }
            CancelReview.Focus();
        });
        KeyDown += (_, e) => { if (e.Key == Key.Escape) { (DataContext as ShellDocumentReviewModel)?.Close(); e.Handled = true; } };
    }
    private void CancelClick(object? sender, RoutedEventArgs e) => (DataContext as ShellDocumentReviewModel)?.Close();
}

public enum DocumentReviewChoice { Cancel, Keep, Load }
public sealed class ShellDocumentReviewModel : ReactiveObject, IDialogContext
{
    private readonly Func<Task>? _copy;
    private string? _error;
    public ShellDocumentReviewModel(IAppLocalizer localizer, bool recovery, string source, string current, string incoming, Func<Task>? copy = null)
    {
        IsRecovery = recovery; Source = source; Current = current; Incoming = incoming; _copy = copy;
        Title = localizer.Get(recovery ? VexL.ReviewRecoveryTitle : VexL.ReviewExternalTitle);
        Description = localizer.Get(recovery ? VexL.ReviewRecoveryHint : VexL.ReviewExternalHint);
        LoadLabel = localizer.Get(recovery ? VexL.ReviewRecover : VexL.ReviewLoad);
        IncomingLabel = localizer.Get(recovery ? VexL.ReviewDraft : VexL.ReviewExternal);
    }
    public bool IsRecovery { get; }
    public string Title { get; }
    public string Description { get; }
    public string Source { get; }
    public string Current { get; }
    public string Incoming { get; }
    public string LoadLabel { get; }
    public string IncomingLabel { get; }
    public double MaximumWidth { get; set; } = 740;
    public double MaximumHeight { get; set; } = 620;
    public string? Error { get => _error; private set => this.RaiseAndSetIfChanged(ref _error, value); }
    public event EventHandler<object?>? RequestClose;
    public void Close() => RequestClose?.Invoke(this, DocumentReviewChoice.Cancel);
    public void Keep() => RequestClose?.Invoke(this, DocumentReviewChoice.Keep);
    public void Load() => RequestClose?.Invoke(this, DocumentReviewChoice.Load);
    public async Task CopyAsync() { try { if (_copy is not null) await _copy(); Error = null; } catch (Exception exception) { Error = exception.Message; } }
}
