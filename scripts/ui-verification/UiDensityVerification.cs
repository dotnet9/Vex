using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Vex.Core.Services;
using Vex.Modules.Shell.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

internal static class UiDensityVerification
{
    public static void MeasurePerformance(MainWindow window, string output, bool theme = false)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        window.WindowState = WindowState.Normal; window.Width = 1392; window.Height = 852;
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        var load = vm.OpenPathAsync(Path.Combine(BundledDocuments.SamplesFolder, "C# 语言之美.md"));
        while (!load.IsCompleted) { Render(); Thread.Sleep(10); }
        load.GetAwaiter().GetResult();
        var results = new List<object>();
        var notifications = 0;
        var host = (IResourceHost)Application.Current!;
        void Changed(object? sender, ResourcesChangedEventArgs args) => notifications++;
        host.ResourcesChanged += Changed;
        try
        {
            foreach (var layout in new[] { "instant", "source-render" })
            {
                vm.Workbench.SelectLayout(layout); Render();
                var appearance = window.GetVisualDescendants().OfType<ShellAppearanceMenuView>().Single();
                appearance.AppearanceButton.Flyout!.ShowAt(appearance.AppearanceButton); Render();
                foreach (var density in theme ? new[] { "dark", "light", "dark", "light" } : new[] { "compact", "relaxed", "normal" })
                {
                    notifications = 0;
                    var watch = System.Diagnostics.Stopwatch.StartNew();
                    if (theme) vm.Appearance.SelectThemeByKey(density);
                    else vm.Appearance.SelectedDensity = density;
                    var setterMs = watch.Elapsed.TotalMilliseconds;
                    Render();
                    var totalMs = watch.Elapsed.TotalMilliseconds;
                    Console.WriteLine($"{(theme ? "THEME" : "DENSITY")} PERF {layout}/{density}: setter={setterMs:0.0}ms frame={totalMs:0.0}ms resources={notifications}");
                    results.Add(new { layout, density, setterMs, totalMs, notifications });
                    if (notifications != (theme ? 0 : 1)) throw new InvalidOperationException(theme
                        ? "Theme changes must use dynamic resources without rewriting application resources."
                        : "A density change must broadcast application resources exactly once.");
                }
                appearance.AppearanceButton.Flyout.Hide(); Render();
            }
            File.WriteAllText(Path.Combine(output, theme ? "theme-performance.json" : "density-performance.json"), System.Text.Json.JsonSerializer.Serialize(results));
            Console.WriteLine(theme ? "PASS Theme changes use dynamic bindings without rewriting application resources"
                : "PASS Density changes use one native batch resource notification per switch");
        }
        finally { host.ResourcesChanged -= Changed; }
    }

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var preferences = app.Resolve<EditorPreferences>();
        void Render()
        {
            Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
            LayoutSettle.Run(); Dispatcher.UIThread.RunJobs(); window.UpdateLayout();
        }
        static void Check(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
            Console.WriteLine("PASS " + message);
        }
        T Find<T>(string name) where T : Control => window.GetVisualDescendants().OfType<T>().First(c => c.Name == name);
        double Top(Control c) => c.TranslatePoint(default, window)!.Value.Y;
        window.WindowState = WindowState.Normal;
        window.Width = 1392; window.Height = 852;
        if (!vm.Layout.ShowOutlineSidebar) vm.Layout.ToggleOutline();
        vm.Appearance.SelectLanguageByCulture("zh-CN");
        vm.Appearance.SelectThemeByKey("light");
        vm.Appearance.SelectedDensity = "normal";
        vm.Workbench.SelectLayout("source-render");
        vm.Markdown = "# C# 与 .NET\n\n## 界面密度\n\n正文、源码与图表保持独立字号。\n";
        Render();
        var bodySize = preferences.BodyFontSize;
        var codeSize = preferences.CodeFontSize;
        var radius = (CornerRadius)Application.Current!.FindResource("VexDensityCornerRadiusMd")!;
        var sourceView = window.GetVisualDescendants().OfType<CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView>().First();
        var source = sourceView.Editor;
        var sourceSize = source.FontSize;
        var files = window.GetVisualDescendants().OfType<ShellFilesView>().Single();
        var outline = window.GetVisualDescendants().OfType<ShellOutlineView>().Single();
        var previousHeader = 0d;
        var previousRail = 0d;
        var previousMenu = 0d;
        foreach (var density in new[] { "compact", "normal", "relaxed" })
        {
            vm.FindBar.CloseFindPanel();
            vm.Appearance.SelectedDensity = density; Render();
            var headers = new[] { files, (Control)outline }.SelectMany(c => c.GetVisualDescendants().OfType<Border>())
                .Where(b => b.Classes.Contains("panel-head") || b.Classes.Contains("panel-tools")).ToArray();
            var tab = Find<Border>("WorkspaceTabRow");
            Check(headers.Length == 4 && headers.All(h => Math.Abs(h.Bounds.Height - tab.Bounds.Height) < 1), density + ": sidebar and workspace rows share a height");
            Check(headers.Where(h => h.Classes.Contains("panel-head")).All(h => Math.Abs(Top(h) - Top(tab)) < 1), density + ": three column titles align");
            var toolbar = window.GetVisualDescendants().OfType<Border>().First(b => b.Classes.Contains("edit-options") && b.IsEffectivelyVisible);
            Check(headers.Where(h => h.Classes.Contains("panel-tools")).All(h => Math.Abs(h.Bounds.Height - toolbar.Bounds.Height) < 1), density + ": search and formatting rows share a height");
            var rail = Find<ToggleButton>("RailFilesToggle");
            Check(tab.Bounds.Height > previousHeader && rail.Bounds.Height > previousRail, density + ": chrome grows between density presets");
            previousHeader = tab.Bounds.Height; previousRail = rail.Bounds.Height;
            vm.FindBar.ShowFindPanel(); Render();
            var find = Find<Border>("FindStrip");
            var input = Find<TextBox>("FindTextBox");
            Check(input.Bounds.Height >= input.DesiredSize.Height - 1 && find.Bounds.Height > input.Bounds.Height, density + ": find input fits inside the strip");
            Check(preferences.BodyFontSize == bodySize && preferences.CodeFontSize == codeSize && source.FontSize == sourceSize,
                density + ": document and source font sizes remain unchanged");
            Check(Equals(Application.Current?.FindResource("VexDensityCornerRadiusMd"), radius), density + ": density does not change corner radius");
            var zoom = window.GetVisualDescendants().OfType<Vex.Controls.VexZoomDock>().First(z => z.Classes.Contains("document-zoom"));
            Check(Math.Abs(zoom.Bounds.Height - (double)app.FindResource("VexUiControlHeight")!) < 1, density + ": zoom dock follows control height");
            var menu = sourceView.ContextMenu!;
            menu.Open(sourceView); Render();
            var rows = menu.Items.OfType<MenuItem>().Where(m => m.IsVisible).ToArray();
            Check(rows.Length > 0 && rows.All(r => r.Bounds.Height >= r.DesiredSize.Height - 1), density + ": context menu text fits its rows");
            Check(rows[0].Bounds.Height > previousMenu, density + ": open context menu uses density spacing");
            previousMenu = rows[0].Bounds.Height;
            menu.Close(); Render();
            FileListTypographyVerification.Run(app, window, output); Render();
            vm.FindBar.CloseFindPanel(); Render();
            window.CaptureRenderedFrame()!.Save(Path.Combine(output, "density-" + density + ".png"));
        }

        var appearance = window.GetVisualDescendants().OfType<ShellAppearanceMenuView>().Single();
        appearance.AppearanceButton.Flyout!.ShowAt(appearance.AppearanceButton); Render();
        var pill = appearance.MenuContent.GetVisualDescendants().OfType<Button>().First(b => b.Classes.Contains("density-pill"));
        var roomyHeight = pill.Bounds.Height;
        vm.Appearance.SelectedDensity = "compact"; Render();
        Check(pill.Bounds.Height < roomyHeight, "Already-open appearance controls update immediately");
        appearance.AppearanceButton.Flyout.Hide(); Render();
        vm.Appearance.SelectedDensity = "relaxed"; Render();
        appearance.AppearanceButton.Flyout.ShowAt(appearance.AppearanceButton); Render();
        Check(Math.Abs(pill.Bounds.Height - roomyHeight) < 1, "Reopened appearance controls use the latest density");
        appearance.AppearanceButton.Flyout.Hide(); Render();

        vm.FindBar.CloseFindPanel();
        var model = app.Resolve<ShellPreferencesViewModel>();
        var dialog = app.Resolve<IShellOverlayService>().ShowPreferencesAsync(model); Render();
        var view = window.GetVisualDescendants().OfType<ShellPreferencesOverlayView>().Single();
        var field = view.GetVisualDescendants().OfType<TemplatedControl>().First(c => c.Classes.Contains("pref-field"));
        var oldHeight = field.Bounds.Height;
        vm.Appearance.SelectedDensity = "compact"; Render();
        Check(field.Bounds.Height < oldHeight, "Already-open preference fields update immediately");
        vm.Appearance.SelectedDensity = "relaxed"; Render();
        Check(field.Bounds.Height >= oldHeight, "Preference fields grow again without reopening");
        window.CaptureRenderedFrame()!.Save(Path.Combine(output, "density-preferences.png"));
        model.Close(); Render(); dialog.GetAwaiter().GetResult();
        vm.Appearance.SelectedDensity = "normal"; Render();
    }
}
