using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Ursa.Controls;

namespace Vex.Modules.Shell.Services;

public sealed class ShellToastPublisher : IShellToastPublisher
{
    private const int MaxItems = 3;
    private static readonly TimeSpan DefaultExpiration = TimeSpan.FromSeconds(2.5);

    private readonly Queue<(string Message, NotificationType Type)> _pending = new();
    private WindowToastManager? _manager;

    public void Install(VisualLayerManager? layerManager)
    {
        if (layerManager is null)
        {
            return;
        }

        _manager = new WindowToastManager(layerManager) { MaxItems = MaxItems };
        while (_pending.Count > 0)
        {
            var (message, type) = _pending.Dequeue();
            Show(message, type);
        }
    }

    public void Publish(string message, NotificationType type = NotificationType.Success)
    {
        if (_manager is null)
        {
            _pending.Enqueue((message, type));
            return;
        }

        Show(message, type);
    }

    private void Show(string message, NotificationType type)
    {
        _manager?.Show(new Toast(message, type, DefaultExpiration)
        {
            ShowIcon = true,
            ShowClose = false
        });
    }
}
