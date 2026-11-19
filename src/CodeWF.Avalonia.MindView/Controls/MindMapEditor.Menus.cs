using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Layout;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.MindView.Controls;

public partial class MindMapEditor
{
    private Border CreateNodeToolbar()
    {
        var panel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = GetResourceDouble(MindViewStyleKeys.ToolbarButtonSpacingResource, 4)
        };
        panel.Children.Add(CreateToolbarButton(
            AddSiblingText,
            Geometry.Parse("M4 7h6v6H4zM14 7h6v6h-6zM10 10h4M7 13v4h10v-4"),
            AddToolbarSiblingNode));
        panel.Children.Add(CreateToolbarButton(
            AddChildText,
            Geometry.Parse("M5 5h7v7H5zM12 8h5v4M17 12h2v7h-7v-7h5"),
            AddToolbarChildNode));
        panel.Children.Add(CreateToolbarButton(
            PromoteText,
            Geometry.Parse("M5 7h14M5 12h9M5 17h5M15 13l4-4-4-4"),
            PromoteToolbarNode));
        panel.Children.Add(CreateToolbarButton(
            DemoteText,
            Geometry.Parse("M5 7h14M10 12h9M14 17h5M9 13l-4-4 4-4"),
            DemoteToolbarNode));
        panel.Children.Add(CreateToolbarButton(
            MoveUpText,
            Geometry.Parse("M12 19V5M6 11l6-6 6 6"),
            MoveToolbarNodeUp));
        panel.Children.Add(CreateToolbarButton(
            MoveDownText,
            Geometry.Parse("M12 5v14M6 13l6 6 6-6"),
            MoveToolbarNodeDown));
        panel.Children.Add(CreateToolbarButton(
            AddNoteText,
            Geometry.Parse("M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"),
            () =>
            {
                if (_toolbarNode is not null)
                {
                    ShowNoteEditor(_toolbarNode);
                }
            }));
        panel.Children.Add(CreateToolbarButton(
            DeleteNodeText,
            Geometry.Parse("M3 6h18M8 6V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6M10 11v6M14 11v6"),
            DeleteToolbarNode));

        return new Border
        {
            Width = GetResourceDouble(MindViewStyleKeys.ToolbarWidthResource, 208),
            Height = GetResourceDouble(MindViewStyleKeys.ToolbarHeightResource, 32),
            Padding = GetResourceThickness(MindViewStyleKeys.ToolbarPaddingResource, new Thickness(8, 3)),
            CornerRadius = GetResourceCornerRadius(MindViewStyleKeys.ToolbarCornerRadiusResource, new CornerRadius(6)),
            Background = GetResourceBrush(MindViewStyleKeys.ToolbarBackgroundBrushResource, "#FFFFFF", "#111827"),
            BorderBrush = GetResourceBrush(MindViewStyleKeys.ToolbarBorderBrushResource, "#D8E0EA", "#334155"),
            BorderThickness = GetResourceThickness(MindViewStyleKeys.ToolbarBorderThicknessResource, new Thickness(1)),
            BoxShadow = GetResourceBoxShadows(MindViewStyleKeys.ToolbarBoxShadowResource, "0 6 18 0 #22000000"),
            Child = panel,
            IsVisible = false
        };
    }

    private Control CreateToolbarButton(string tooltip, Geometry icon, Action action)
    {
        var iconSize = GetResourceDouble(MindViewStyleKeys.ToolbarIconSizeResource, 15);
        var path = new global::Avalonia.Controls.Shapes.Path
        {
            Data = icon,
            Stroke = GetSecondaryTextBrush(),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };
        var button = new Button
        {
            Width = GetResourceDouble(MindViewStyleKeys.ToolbarButtonWidthResource, 28),
            Height = GetResourceDouble(MindViewStyleKeys.ToolbarButtonHeightResource, 24),
            MinWidth = GetResourceDouble(MindViewStyleKeys.ToolbarButtonWidthResource, 28),
            MinHeight = GetResourceDouble(MindViewStyleKeys.ToolbarButtonHeightResource, 24),
            Padding = new Thickness(0),
            Content = new Viewbox
            {
                Width = iconSize,
                Height = iconSize,
                Child = path
            }
        };
        ToolTip.SetTip(button, tooltip);
        button.Click += (_, e) =>
        {
            action();
            e.Handled = true;
        };
        return button;
    }

    private Button CreateNodeAddChildButton()
    {
        var icon = new global::Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M12 5v14M5 12h14"),
            Stroke = GetPrimaryTextBrush(),
            StrokeThickness = 2.2,
            Fill = Brushes.Transparent,
            Stretch = Stretch.Uniform,
            StrokeLineCap = PenLineCap.Round,
            StrokeJoin = PenLineJoin.Round
        };
        var button = new Button
        {
            Width = 28,
            Height = 28,
            MinWidth = 28,
            MinHeight = 28,
            Padding = new Thickness(0),
            Content = new Viewbox
            {
                Width = 14,
                Height = 14,
                Child = icon
            },
            IsVisible = false
        };
        ToolTip.SetTip(button, AddChildText);
        button.Click += (_, e) =>
        {
            AddToolbarChildNode();
            e.Handled = true;
        };
        return button;
    }

    private Border CreateNodeMenu()
    {
        var menu = new Border
        {
            Width = NodeMenuWidth,
            Padding = GetResourceThickness(MindViewStyleKeys.NodeMenuPaddingResource, new Thickness(6)),
            CornerRadius = GetResourceCornerRadius(MindViewStyleKeys.NodeMenuCornerRadiusResource, new CornerRadius(6)),
            Background = GetResourceBrush(MindViewStyleKeys.ToolbarBackgroundBrushResource, "#FFFFFF", "#111827"),
            BorderBrush = GetResourceBrush(MindViewStyleKeys.ToolbarBorderBrushResource, "#D8E0EA", "#334155"),
            BorderThickness = GetResourceThickness(MindViewStyleKeys.NodeMenuBorderThicknessResource, new Thickness(1)),
            BoxShadow = GetResourceBoxShadows(MindViewStyleKeys.NodeMenuBoxShadowResource, "0 8 22 0 #24000000"),
            Child = _nodeMenuPanel,
            IsVisible = false
        };
        menu.PointerPressed += (_, e) => e.Handled = true;
        return menu;
    }

    private void ShowNodeMenu(MindMapNode node, Point canvasPoint)
    {
        _nodeMenuPanel.Children.Clear();
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("+", AddChildText, "Tab / Shift+Tab", true, () => AddChildFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("+", AddSiblingText, "Enter", !IsRootNode(node), () => AddSiblingFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("<", PromoteText, null, CanPromoteNode(node), () => PromoteNodeFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem(">", DemoteText, null, CanDemoteNode(node), () => DemoteNodeFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("^", MoveUpText, "Alt+Up", CanMoveNodeUp(node), () => MoveNodeUpFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("v", MoveDownText, "Alt+Down", CanMoveNodeDown(node), () => MoveNodeDownFromMenu(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("i", string.IsNullOrWhiteSpace(node.Note) ? AddNoteText : EditNoteText, null, true, () => ShowNoteEditor(node)));
        _nodeMenuPanel.Children.Add(CreateNodeMenuItem("x", DeleteNodeText, "Delete", !IsRootNode(node), () => DeleteNodeFromMenu(node)));

        _nodeMenu.Background = GetResourceBrush(MindViewStyleKeys.ToolbarBackgroundBrushResource, "#FFFFFF", "#111827");
        _nodeMenu.BorderBrush = GetResourceBrush(MindViewStyleKeys.ToolbarBorderBrushResource, "#D8E0EA", "#334155");
        _nodeMenu.IsVisible = true;

        var x = Math.Clamp(canvasPoint.X, 8, Math.Max(8, _canvas.Width - NodeMenuWidth - 8));
        var y = Math.Clamp(canvasPoint.Y, 8, Math.Max(8, _canvas.Height - 270));
        Canvas.SetLeft(_nodeMenu, x);
        Canvas.SetTop(_nodeMenu, y);
    }

    private Control CreateNodeMenuItem(string iconText, string header, string? shortcut, bool isEnabled, Action action)
    {
        var foreground = isEnabled
            ? GetPrimaryTextBrush()
            : GetResourceBrush(MindViewStyleKeys.NodeMenuDisabledTextBrushResource, "#A0A7B1", "#64748B");
        var shortcutBrush = isEnabled
            ? GetResourceBrush(MindViewStyleKeys.NodeMenuShortcutBrushResource, "#667085", "#94A3B8")
            : GetResourceBrush(MindViewStyleKeys.NodeMenuDisabledShortcutBrushResource, "#A0A7B1", "#475569");
        var icon = new TextBlock
        {
            Text = iconText,
            Width = GetResourceDouble(MindViewStyleKeys.NodeMenuIconWidthResource, 18),
            FontSize = GetResourceDouble(MindViewStyleKeys.NodeMenuTextFontSizeResource, 13),
            FontWeight = FontWeight.SemiBold,
            Foreground = shortcutBrush,
            TextAlignment = TextAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        var text = new TextBlock
        {
            Text = header,
            FontSize = GetResourceDouble(MindViewStyleKeys.NodeMenuTextFontSizeResource, 13),
            Foreground = foreground,
            VerticalAlignment = VerticalAlignment.Center
        };
        var shortcutText = new TextBlock
        {
            Text = shortcut ?? string.Empty,
            FontSize = GetResourceDouble(MindViewStyleKeys.NodeMenuShortcutFontSizeResource, 12),
            Foreground = shortcutBrush,
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center
        };
        var content = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("22,*,Auto"),
            ColumnSpacing = 8
        };
        Grid.SetColumn(text, 1);
        Grid.SetColumn(shortcutText, 2);
        content.Children.Add(icon);
        content.Children.Add(text);
        content.Children.Add(shortcutText);

        var row = new Button
        {
            Height = GetResourceDouble(MindViewStyleKeys.NodeMenuRowHeightResource, 30),
            MinHeight = GetResourceDouble(MindViewStyleKeys.NodeMenuRowHeightResource, 30),
            Padding = GetResourceThickness(MindViewStyleKeys.NodeMenuRowPaddingResource, new Thickness(8, 0)),
            IsEnabled = isEnabled,
            HorizontalContentAlignment = HorizontalAlignment.Stretch,
            Cursor = isEnabled ? new Cursor(StandardCursorType.Hand) : Cursor.Default,
            Content = content
        };
        if (isEnabled)
        {
            row.Click += (_, e) =>
            {
                HideNodeMenu();
                action();
                e.Handled = true;
            };
        }

        return row;
    }

    private void HideNodeMenu()
    {
        _nodeMenu.IsVisible = false;
    }

    private void AddToolbarChildNode()
    {
        if (_toolbarNode is null)
        {
            return;
        }

        AddChildFromMenu(_toolbarNode);
    }

    private void AddToolbarSiblingNode()
    {
        if (_toolbarNode is null || IsRootNode(_toolbarNode))
        {
            return;
        }

        AddSiblingFromMenu(_toolbarNode);
    }

    private void PromoteToolbarNode()
    {
        if (_toolbarNode is not null)
        {
            PromoteNodeFromMenu(_toolbarNode);
        }
    }

    private void DemoteToolbarNode()
    {
        if (_toolbarNode is not null)
        {
            DemoteNodeFromMenu(_toolbarNode);
        }
    }

    private void MoveToolbarNodeUp()
    {
        if (_toolbarNode is not null)
        {
            MoveNodeUpFromMenu(_toolbarNode);
        }
    }

    private void MoveToolbarNodeDown()
    {
        if (_toolbarNode is not null)
        {
            MoveNodeDownFromMenu(_toolbarNode);
        }
    }

    private void AddChildFromMenu(MindMapNode node)
    {
        var child = AddChild(node);
        _toolbarNode = child;
        FocusNode(child);
    }

    private void AddSiblingFromMenu(MindMapNode node)
    {
        if (IsRootNode(node))
        {
            return;
        }

        var sibling = AddSibling(node);
        _toolbarNode = sibling;
        FocusNode(sibling);
    }

    private void PromoteNodeFromMenu(MindMapNode node)
    {
        if (PromoteNode(node))
        {
            _toolbarNode = node;
            FocusNode(node);
        }
    }

    private void DemoteNodeFromMenu(MindMapNode node)
    {
        if (DemoteNode(node))
        {
            _toolbarNode = node;
            FocusNode(node);
        }
    }

    private void MoveNodeUpFromMenu(MindMapNode node)
    {
        if (MoveNodeUp(node))
        {
            _toolbarNode = node;
            FocusNode(node);
        }
    }

    private void MoveNodeDownFromMenu(MindMapNode node)
    {
        if (MoveNodeDown(node))
        {
            _toolbarNode = node;
            FocusNode(node);
        }
    }

    private void DeleteNodeFromMenu(MindMapNode node)
    {
        if (IsRootNode(node))
        {
            return;
        }

        var focusTarget = DeleteNode(node);
        _toolbarNode = null;
        UpdateToolbarVisibility();
        FocusNode(focusTarget);
    }

    private void DeleteToolbarNode()
    {
        if (_toolbarNode is null)
        {
            return;
        }

        DeleteNodeFromMenu(_toolbarNode);
    }
}
