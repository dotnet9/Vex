using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
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

        _manager = new WindowToastManager(layerManager)
        {
            MaxItems = MaxItems,
            IsHitTestVisible = false,
        };

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

    public void Uninstall()
    {
        _pending.Clear();
        if (_manager is null) return;
        // Ursa 2.2 的退场动画在卸载时会修改父集合；先逐项完成关闭，再卸载承载层。
        foreach (var card in _manager.GetVisualDescendants().OfType<ToastCard>().ToArray())
        {
            card.Close();
            card.IsClosed = true;
        }
        _manager.Uninstall();
        _manager = null;
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
