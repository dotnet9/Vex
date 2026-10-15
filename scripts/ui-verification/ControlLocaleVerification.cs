using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using CodeWF.Toolkit.Lang;

/// <summary>通过同一个实际编辑菜单检查全局切语、主题资源和宿主覆盖。</summary>
internal static class ControlLocaleVerification
{
    public static void Run(Window owner, string[] cultures, Action<string>? selectCulture = null)
    {
        var app = Application.Current!;
        var manager = I18nManager.Instance;
        var originalCulture = manager.Culture;
        var failures = new List<string>();
        var editor = new TextBox { Text = "locale verification" };
        var window = new Window { Content = editor, Width = 320, Height = 100 };
        window.Show(owner);
        Dispatcher.UIThread.RunJobs();
        var menu = editor.ContextFlyout as MenuFlyout ?? throw new InvalidOperationException("Semi editor menu is missing.");
        var hadOverride = app.Resources.TryGetValue("STRING_MENU_COPY", out var originalOverride);
        try
        {
            foreach (var culture in cultures.Concat(cultures.Reverse()))
            {
                manager.Culture = CultureInfo.GetCultureInfo(culture);
                Dispatcher.UIThread.RunJobs();
                ResourceDictionary semi = culture switch
                {
                    "en-US" => new Semi.Avalonia.Locale.en_us(),
                    "ja-JP" => new Semi.Avalonia.Locale.ja_jp(),
                    "zh-Hant" => new Semi.Avalonia.Locale.zh_tw(),
                    _ => new Semi.Avalonia.Locale.zh_cn()
                };
                ResourceDictionary ursa = culture switch
                {
                    "en-US" => new Ursa.Themes.Semi.Locale.en_us(),
                    "ja-JP" => new Ursa.Themes.Semi.Locale.en_us(),
                    _ => new Ursa.Themes.Semi.Locale.zh_cn()
                };
                menu.ShowAt(editor);
                Dispatcher.UIThread.RunJobs();
                Verify(((MenuItem)menu.Items[1]!).Header?.ToString() == semi["STRING_MENU_COPY"]?.ToString()
                    && ((MenuItem)menu.Items[2]!).Header?.ToString() == semi["STRING_MENU_PASTE"]?.ToString(),
                    "Existing Semi copy/paste menu follows direct global culture: " + culture);
                menu.Hide();
                var theme = app.Styles.OfType<Ursa.Themes.Semi.UrsaSemiTheme>().Single();
                Verify(theme.Resources["STRING_MENU_DIALOG_CANCEL"]?.ToString() == ursa["STRING_MENU_DIALOG_CANCEL"]?.ToString(),
                    "Existing Ursa dialog resources follow direct global culture: " + culture);
            }
            app.Resources["STRING_MENU_COPY"] = "host copy override";
            foreach (var culture in cultures)
            {
                if (selectCulture is null) manager.Culture = CultureInfo.GetCultureInfo(culture);
                else selectCulture(culture);
                Dispatcher.UIThread.RunJobs();
                menu.ShowAt(editor);
                Dispatcher.UIThread.RunJobs();
                Verify(((MenuItem)menu.Items[1]!).Header?.ToString() == "host copy override",
                    "Host copy override survives culture changes: " + culture);
                menu.Hide();
            }
        }
        finally
        {
            if (hadOverride) app.Resources["STRING_MENU_COPY"] = originalOverride;
            else app.Resources.Remove("STRING_MENU_COPY");
            manager.Culture = originalCulture;
            window.Close();
        }
        if (failures.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, failures));

        void Verify(bool passed, string message)
        {
            Console.WriteLine((passed ? "PASS " : "FAIL ") + message);
            if (!passed) failures.Add(message);
        }
    }
}
