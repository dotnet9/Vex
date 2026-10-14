using Avalonia;
using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.ReactiveUI;
using Avalonia.Threading;
using CodeWF.Toolkit.Core.UpdateChecking;
using Vex;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;

var directory = Path.GetFullPath(args.FirstOrDefault() ?? "artifacts/about-visual-verification");
Directory.CreateDirectory(directory);
AppBuilder.Configure<App>().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
    .UseSkia().WithInterFont().UseReactiveUI().SetupWithoutStarting();
var localizer = new AppLocalizer();
foreach (var culture in new[] { "zh-CN", "en-US", "zh-Hant", "ja-JP" })
{
    localizer.SetCulture(culture);
    using var updates = new UpdateService(new UpdateServiceOptions { Repository = "Vex", ProductName = "Vex" });
    using var model = new ShellAboutViewModel(updates, localizer, _ => { }, () => { });
    if (model.VersionText != UpdateVersion.Display(typeof(App).Assembly) || !DateTime.TryParseExact(model.CompileTimeText, "yyyy-MM-dd HH:mm:ss",
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var builtAt) || builtAt < DateTime.Now.AddDays(-1) || builtAt > DateTime.Now.AddMinutes(5))
        throw new InvalidOperationException("About version or build timestamp is incorrect: " + model.VersionText + ", " + model.CompileTimeText);
    var view = new ShellAboutOverlayView { DataContext = model };
    var window = new Window { Width = 640, Height = 760, Content = view };
    window.Show();
    window.UpdateLayout();
    Dispatcher.UIThread.RunJobs();
    if (view.Bounds.Width <= 0 || view.Bounds.Height <= 0 || model.ActionText.StartsWith("Vex.VexL."))
        throw new InvalidOperationException("About view or localized binding failed: " + culture);
    using var bitmap = new RenderTargetBitmap(new PixelSize(640, 760));
    bitmap.Render(window);
    bitmap.Save(Path.Combine(directory, "about-" + culture + ".png"), new PngBitmapEncoderOptions());
    window.Close();
    Console.WriteLine("PASS: rendered about overlay " + culture + ", version " + model.VersionText + ", built " + model.CompileTimeText);
}
