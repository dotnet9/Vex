using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Vex.Core.Behaviors;

public static class FocusOnVisible
{
    public static readonly AttachedProperty<string?> TargetNameProperty =
        AvaloniaProperty.RegisterAttached<Control, string?>("TargetName", typeof(FocusOnVisible));

    public static readonly AttachedProperty<bool> SelectAllProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("SelectAll", typeof(FocusOnVisible), defaultValue: true);

    private static readonly AttachedProperty<FocusSubscription?> SubscriptionProperty =
        AvaloniaProperty.RegisterAttached<Control, FocusSubscription?>("Subscription", typeof(FocusOnVisible));

    static FocusOnVisible()
    {
        TargetNameProperty.Changed.AddClassHandler<Control, string?>(OnTargetNameChanged);
    }

    public static string? GetTargetName(Control control) => control.GetValue(TargetNameProperty);

    public static void SetTargetName(Control control, string? value) => control.SetValue(TargetNameProperty, value);

    public static bool GetSelectAll(Control control) => control.GetValue(SelectAllProperty);

    public static void SetSelectAll(Control control, bool value) => control.SetValue(SelectAllProperty, value);

    private static FocusSubscription? GetSubscription(Control control) => control.GetValue(SubscriptionProperty);

    private static void SetSubscription(Control control, FocusSubscription? value)
    {
        control.SetValue(SubscriptionProperty, value);
    }

    private static void OnTargetNameChanged(Control control, AvaloniaPropertyChangedEventArgs<string?> args)
    {
        GetSubscription(control)?.Dispose();
        SetSubscription(control, null);

        if (string.IsNullOrWhiteSpace(args.NewValue.Value))
        {
            return;
        }

        var subscription = new FocusSubscription(control);
        SetSubscription(control, subscription);
        subscription.Start();
    }

    private sealed class FocusSubscription : IDisposable
    {
        private readonly Control _host;
        private IDisposable? _visibilitySubscription;

        public FocusSubscription(Control host)
        {
            _host = host;
        }

        public void Start()
        {
            _host.AttachedToVisualTree += OnAttachedToVisualTree;
            _visibilitySubscription = _host.GetObservable(Visual.IsVisibleProperty).Subscribe(OnHostVisibilityChanged);
            QueueFocusWhenVisible();
        }

        public void Dispose()
        {
            _host.AttachedToVisualTree -= OnAttachedToVisualTree;
            _visibilitySubscription?.Dispose();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            QueueFocusWhenVisible();
        }

        private void OnHostVisibilityChanged(bool isVisible)
        {
            if (isVisible)
            {
                QueueFocusWhenVisible();
            }
        }

        private void QueueFocusWhenVisible()
        {
            if (!_host.IsEffectivelyVisible)
            {
                return;
            }

            Dispatcher.UIThread.Post(() =>
            {
                if (!_host.IsEffectivelyVisible)
                {
                    return;
                }

                var target = ResolveTarget();
                if (target is null)
                {
                    return;
                }

                // 显示完成后再聚焦，避免可见性切换过程中焦点被布局刷新吞掉。
                target.Focus();
                if (GetSelectAll(_host) && target is TextBox textBox)
                {
                    textBox.SelectAll();
                }
            });
        }

        private Control? ResolveTarget()
        {
            var targetName = GetTargetName(_host);
            return string.IsNullOrWhiteSpace(targetName)
                ? _host
                : _host.FindControl<Control>(targetName);
        }
    }
}
