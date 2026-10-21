using System.Reactive;
using System.Reflection;
using CodeWF.Toolkit.Core.Extensions;
using CodeWF.Toolkit.Core.UpdateChecking;
using Irihi.Avalonia.Shared.Contracts;
using ReactiveUI;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellAboutViewModel : ReactiveObject, IDialogContext, IDisposable
{
    public ShellAboutViewModel(IAppLocalizer localizer, Action<string> openUrl, Assembly? assembly = null)
    {
        assembly ??= typeof(App).Assembly;
        VersionText = UpdateVersion.Display(assembly);
        PlatformText = ".NET 10，Avalonia";
        CompileTimeText = assembly.CompileTime()?.ToString("yyyy-MM-dd HH:mm:ss", localizer.Culture) ?? "—";
        CopyrightText = "Copyright © 2026 CodeWF · 维刻 Vex";
        ReleaseCommand = ReactiveCommand.Create(() => openUrl("https://github.com/dotnet9/Vex/releases/tag/v" + VersionText.TrimStart('v')));
        RepositoryCommand = ReactiveCommand.Create(() => openUrl("https://github.com/dotnet9/Vex"));
        WebsiteCommand = ReactiveCommand.Create(() => openUrl("https://codewf.com"));
        ChangelogCommand = ReactiveCommand.Create(() => { WantsChangelog = true; Close(); });
        UpdateCommand = ReactiveCommand.Create(() => { WantsUpdates = true; Close(); });
    }

    public bool WantsChangelog { get; private set; }
    public bool WantsUpdates { get; private set; }
    public string VersionText { get; }
    public string PlatformText { get; }
    public string CompileTimeText { get; }
    public string CopyrightText { get; }
    public ReactiveCommand<Unit, Unit> ChangelogCommand { get; }
    public ReactiveCommand<Unit, Unit> UpdateCommand { get; }
    public ReactiveCommand<Unit, Unit> RepositoryCommand { get; }
    public ReactiveCommand<Unit, Unit> ReleaseCommand { get; }
    public ReactiveCommand<Unit, Unit> WebsiteCommand { get; }
    public event EventHandler<object?>? RequestClose;
    public void Close() => RequestClose?.Invoke(this, null);
    public void Dispose()
    {
        ChangelogCommand.Dispose();
        UpdateCommand.Dispose();
        RepositoryCommand.Dispose();
        ReleaseCommand.Dispose();
        WebsiteCommand.Dispose();
    }
}
