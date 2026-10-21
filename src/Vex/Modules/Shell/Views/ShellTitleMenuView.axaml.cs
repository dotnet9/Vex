using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Controls.Primitives;
using Avalonia.VisualTree;
using Avalonia.Threading;
using CodeWF.Toolkit.Lang;
using Vex.Modules.Shell.ViewModels;

namespace Vex.Modules.Shell.Views;

public partial class ShellTitleMenuView : UserControl
{
    private ShellTitleMenuViewModel? _model;
    private readonly List<(MenuItem Item, Func<string> Label, Func<bool> Selected)> _choices = [];
    public ShellTitleMenuView()
    {
        InitializeComponent();
        foreach (var item in ((Menu)Content!).Items.OfType<MenuItem>()) Configure(item, true);
        DataContextChanged += (_, _) => AttachModel();
        AttachedToVisualTree += (_, _) => { I18nManager.Instance.CultureChanged += CultureChanged; AttachModel(); };
        DetachedFromVisualTree += (_, _) =>
        {
            I18nManager.Instance.CultureChanged -= CultureChanged;
            if (_model is not null) _model.Appearance.PropertyChanged -= AppearanceChanged;
            _model = null;
        };
    }

    private void Configure(MenuItem item, bool topLevel = false)
    {
        if (!topLevel) item.Classes.Add("design-menu-entry");
        item.SubmenuOpened += (_, _) =>
            {
                var popup = item.GetVisualDescendants().OfType<Popup>().FirstOrDefault();
                if (popup?.Child is not { } panel) return;
                panel.Width = ShellAppearanceMenuView.MenuWidth;
                panel.MaxHeight = Math.Max(120, (TopLevel.GetTopLevel(this)?.ClientSize.Height ?? 820) - 56);
            };
        foreach (var child in item.Items.OfType<MenuItem>()) Configure(child);
    }

    private void AttachModel()
    {
        if (ReferenceEquals(_model, DataContext)) return;
        if (_model is not null) _model.Appearance.PropertyChanged -= AppearanceChanged;
        _model = DataContext as ShellTitleMenuViewModel;
        if (_model is null) return;
        _model.Appearance.PropertyChanged += AppearanceChanged;
        _choices.Clear(); ThemeMenuItem.Items.Clear(); TypographyMenuItem.Items.Clear();
        var appearance = _model.Appearance;
        foreach (var mode in appearance.ThemeOptions)
            AddChoice(ThemeMenuItem, () => DesignSelectionControls.Text(mode.Key switch { "light" => "ThemeLight", "dark" => "ThemeDark", _ => "ThemeSystem" }),
                () => appearance.SelectedTheme == mode, () => appearance.SelectedTheme = mode);
        ThemeMenuItem.Items.Add(new Separator());
        foreach (var palette in appearance.PaletteOptions)
            AddChoice(ThemeMenuItem, () => palette.Name, () => appearance.SelectedPalette == palette, () => appearance.SelectedPalette = palette);
        foreach (var typography in appearance.TypographyOptions)
            AddChoice(TypographyMenuItem, () => DesignSelectionControls.Text(typography.NameKey), () => appearance.SelectedTypography == typography, () => appearance.SelectedTypography = typography);
        TypographyMenuItem.Items.Add(new Separator());
        AddChoice(TypographyMenuItem, () => DesignSelectionControls.Text("CompactLayout"), () => appearance.IsCompactLayout, () => appearance.IsCompactLayout = !appearance.IsCompactLayout);
        RefreshChoices();
    }

    private void AddChoice(MenuItem parent, Func<string> label, Func<bool> selected, Action apply)
    {
        var item = new MenuItem { ToggleType = MenuItemToggleType.CheckBox };
        item.Click += (_, _) => { apply(); Dispatcher.UIThread.Post(RefreshChoices); };
        Configure(item); _choices.Add((item, label, selected)); parent.Items.Add(item);
    }

    private void CultureChanged(object? sender, EventArgs e) => Dispatcher.UIThread.Post(RefreshChoices);
    private void AppearanceChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e) => RefreshChoices();
    private void RefreshChoices()
    {
        foreach (var (item, label, selected) in _choices) { item.Header = label(); item.IsChecked = selected(); }
    }

    public event EventHandler? BeginGuideRequested;

    public MenuItem FileMenuTarget => FileMenuItem;

    private void BeginGuideMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginGuideRequested?.Invoke(this, EventArgs.Empty);
    }
}
