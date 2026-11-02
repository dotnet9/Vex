using RxVoid = ReactiveUI.Primitives.RxVoid;
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
        PlatformText = $".NET {Environment.Version.Major}，Avalonia";
        CompileTimeText = assembly.CompileTime()?.ToString("yyyy-MM-dd HH:mm:ss", localizer.Culture) ?? "—";
        CopyrightText = "Copyright © 2026 CodeWF · 维刻 Vex";
        ReleaseCommand = ReactiveCommand.Create(() => openUrl("https://github.com/dotnet9/Vex/releases/tag/v" + VersionText.TrimStart('v')));
        RepositoryCommand = ReactiveCommand.Create(() => openUrl("https://github.com/dotnet9/Vex"));
        WebsiteCommand = ReactiveCommand.Create(() => openUrl(ProjectLinks.Website));
        ChangelogCommand = ReactiveCommand.Create(() => { WantsChangelog = true; Close(); });
        UpdateCommand = ReactiveCommand.Create(() => { WantsUpdates = true; Close(); });
    }

    public bool WantsChangelog { get; private set; }
    public bool WantsUpdates { get; private set; }
    public string VersionText { get; }
    public string PlatformText { get; }
    public string CompileTimeText { get; }
    public string CopyrightText { get; }
    public ReactiveCommand<RxVoid, RxVoid> ChangelogCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> UpdateCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> RepositoryCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> ReleaseCommand { get; }
    public ReactiveCommand<RxVoid, RxVoid> WebsiteCommand { get; }
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
