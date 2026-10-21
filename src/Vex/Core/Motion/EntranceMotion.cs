using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Vex.Core.Motion;

/// <summary>只动画合成属性，不在逐帧动画中重新排版正文。</summary>
public sealed class EntranceMotion : AvaloniaObject
{
    public static readonly AttachedProperty<bool> IsEnabledProperty =
        AvaloniaProperty.RegisterAttached<EntranceMotion, Control, bool>("IsEnabled");
    public static readonly AttachedProperty<bool> FadeOnlyProperty =
        AvaloniaProperty.RegisterAttached<EntranceMotion, Control, bool>("FadeOnly");
    private static readonly AttachedProperty<State?> StateProperty =
        AvaloniaProperty.RegisterAttached<EntranceMotion, Control, State?>("State");

    static EntranceMotion() => IsEnabledProperty.Changed.AddClassHandler<Control>((control, change) =>
    {
        control.GetValue(StateProperty)?.Dispose();
        control.SetValue(StateProperty, control.GetValue(IsEnabledProperty) ? new State(control) : null);
    });

    public static bool GetIsEnabled(Control control) => control.GetValue(IsEnabledProperty);
    public static void SetIsEnabled(Control control, bool value) => control.SetValue(IsEnabledProperty, value);
    public static bool GetFadeOnly(Control control) => control.GetValue(FadeOnlyProperty);
    public static void SetFadeOnly(Control control, bool value) => control.SetValue(FadeOnlyProperty, value);

    private sealed class State : IDisposable
    {
        private readonly Control _control;
        private CancellationTokenSource? _run;
        private bool _attached;
        private bool _visible;
        private bool _checkPending;
        private bool _disposed;
        private readonly List<Visual> _ancestors = [];

        public State(Control control)
        {
            _control = control;
            control.AttachedToVisualTree += Attached;
            control.DetachedFromVisualTree += Detached;
            if (control.IsAttachedToVisualTree()) Attach();
        }

        private void Attached(object? sender, VisualTreeAttachmentEventArgs e) => Attach();
        private void Attach()
        {
            if (_attached) return;
            _attached = true;
            for (Visual? current = _control; current is not null; current = current.GetVisualParent())
            {
                _ancestors.Add(current);
                current.PropertyChanged += VisibilityChanged;
            }
            MotionResources.Changed += MotionChanged;
            QueueVisibilityCheck();
        }
        private void Detached(object? sender, VisualTreeAttachmentEventArgs e) => Detach();
        private void Detach()
        {
            _attached = false;
            foreach (var visual in _ancestors) visual.PropertyChanged -= VisibilityChanged;
            _ancestors.Clear();
            _visible = false;
            MotionResources.Changed -= MotionChanged;
            Cancel();
        }
        private void VisibilityChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Visual.IsVisibleProperty)
                QueueVisibilityCheck();
        }
        private void QueueVisibilityCheck()
        {
            if (_checkPending || _disposed) return;
            _checkPending = true;
            Dispatcher.UIThread.Post(() =>
            {
                _checkPending = false;
                if (!_disposed) CheckVisibility();
            }, DispatcherPriority.Loaded);
        }
        private void CheckVisibility()
        {
            var visible = _attached && _control.IsEffectivelyVisible;
            if (_visible == visible) return;
            _visible = visible;
            if (visible) Start(); else Cancel();
        }
        private void MotionChanged() { if (!MotionResources.IsEnabled) Cancel(); }
        private void Cancel() { _run?.Cancel(); _run?.Dispose(); _run = null; }
        private void Start()
        {
            Cancel();
            if (!MotionResources.IsEnabled) return;
            var dialog = _control.Classes.Contains("dialog-card");
            var first = new KeyFrame { Cue = new Cue(0), Setters = { new Setter(Visual.OpacityProperty, 0d) } };
            var last = new KeyFrame { Cue = new Cue(1), Setters = { new Setter(Visual.OpacityProperty, 1d) } };
            if (!GetFadeOnly(_control) && _control is not MenuFlyoutPresenter)
            {
                first.Setters.Add(new Setter(TranslateTransform.YProperty, dialog ? 9d : -4d));
                first.Setters.Add(new Setter(ScaleTransform.ScaleXProperty, dialog ? .99 : .985));
                first.Setters.Add(new Setter(ScaleTransform.ScaleYProperty, dialog ? .99 : .985));
                last.Setters.Add(new Setter(TranslateTransform.YProperty, 0d));
                last.Setters.Add(new Setter(ScaleTransform.ScaleXProperty, 1d));
                last.Setters.Add(new Setter(ScaleTransform.ScaleYProperty, 1d));
            }
            var animation = new Animation
            {
                Duration = TimeSpan.FromMilliseconds(dialog ? 220 : 140),
                Easing = new SplineEasing { X1 = .2, Y1 = .8, X2 = .2, Y2 = 1 },
                Children = { first, last }
            };
            _run = new CancellationTokenSource();
            _ = Run(animation, _run.Token);
        }
        private async Task Run(Animation animation, CancellationToken token)
        {
            try { await animation.RunAsync(_control, token); }
            catch (OperationCanceledException) { }
        }
        public void Dispose()
        {
            _disposed = true;
            Detach();
            _control.AttachedToVisualTree -= Attached;
            _control.DetachedFromVisualTree -= Detached;
        }
    }
}
