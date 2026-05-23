using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;

namespace Vex.Modules.Shell.Views;

public partial class ShellTitleMenuView : UserControl
{
    public const string FileGuideMenu = "file";
    public const string ParagraphGuideMenu = "paragraph";
    public const string FormatGuideMenu = "format";
    public const string ViewGuideMenu = "view";
    public const string ThemeColorGuideMenu = "theme-color";
    public const string HelpGuideMenu = "help";

    public ShellTitleMenuView()
    {
        InitializeComponent();
    }

    public event EventHandler? BeginGuideRequested;

    public MenuItem FileMenuTarget => FileMenuItem;

    public MenuItem OpenFolderMenuTarget => OpenFolderMenuItem;

    public MenuItem ExportMenuTarget => ExportMenuItem;

    public MenuItem TableMenuTarget => TableMenuItem;

    public MenuItem LinkMenuTarget => LinkMenuItem;

    public MenuItem SourceModeMenuTarget => SourceModeMenuItem;

    public MenuItem OutlineMenuTarget => OutlineMenuItem;

    public MenuItem ThemeDarkMenuTarget => ThemeDarkMenuItem;

    public MenuItem BeginGuideMenuTarget => BeginGuideMenuItem;

    public void SetGuideMenuOpen(string? menuKey)
    {
        CloseGuideMenus();
        if (string.IsNullOrWhiteSpace(menuKey))
        {
            return;
        }

        ApplyGuideMenuOpen(menuKey);
        Dispatcher.UIThread.Post(() => ApplyGuideMenuOpen(menuKey), DispatcherPriority.Background);
    }

    public void CloseGuideMenus()
    {
        ExportMenuItem.IsSubMenuOpen = false;
        ThemeColorMenuItem.IsSubMenuOpen = false;
        ThemeMenuItem.IsSubMenuOpen = false;
        LanguageMenuItem.IsSubMenuOpen = false;
        FileMenuItem.IsSubMenuOpen = false;
        ParagraphMenuItem.IsSubMenuOpen = false;
        FormatMenuItem.IsSubMenuOpen = false;
        ViewMenuItem.IsSubMenuOpen = false;
        HelpMenuItem.IsSubMenuOpen = false;
    }

    private void ApplyGuideMenuOpen(string menuKey)
    {
        switch (menuKey)
        {
            case FileGuideMenu:
                FileMenuItem.IsSubMenuOpen = true;
                break;
            case ParagraphGuideMenu:
                ParagraphMenuItem.IsSubMenuOpen = true;
                break;
            case FormatGuideMenu:
                FormatMenuItem.IsSubMenuOpen = true;
                break;
            case ViewGuideMenu:
                ViewMenuItem.IsSubMenuOpen = true;
                break;
            case ThemeColorGuideMenu:
                HelpMenuItem.IsSubMenuOpen = true;
                ThemeMenuItem.IsSubMenuOpen = true;
                ThemeColorMenuItem.IsSubMenuOpen = true;
                break;
            case HelpGuideMenu:
                HelpMenuItem.IsSubMenuOpen = true;
                break;
        }
    }

    private void BeginGuideMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        BeginGuideRequested?.Invoke(this, EventArgs.Empty);
    }
}
