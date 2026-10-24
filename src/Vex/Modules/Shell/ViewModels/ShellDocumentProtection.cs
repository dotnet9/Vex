using ReactiveUI;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

public sealed class ShellDocumentProtection : ReactiveObject
{
    private string? _saveError, _exportError, _draftError, _fileReadError;
    private DocumentSnapshot? _recovery, _external;
    public string? SaveError { get => _saveError; set { this.RaiseAndSetIfChanged(ref _saveError, value); RaiseDerivedState(); } }
    public string? ExportError { get => _exportError; set { this.RaiseAndSetIfChanged(ref _exportError, value); RaiseDerivedState(); } }
    public string? DraftError { get => _draftError; set { this.RaiseAndSetIfChanged(ref _draftError, value); RaiseDerivedState(); } }
    public string? FileReadError { get => _fileReadError; set { this.RaiseAndSetIfChanged(ref _fileReadError, value); RaiseDerivedState(); } }
    public DocumentSnapshot? Recovery { get => _recovery; set { this.RaiseAndSetIfChanged(ref _recovery, value); RaiseDerivedState(); } }
    public DocumentSnapshot? External { get => _external; set { this.RaiseAndSetIfChanged(ref _external, value); RaiseDerivedState(); } }
    public bool HasSaveError => !string.IsNullOrEmpty(SaveError);
    public bool HasExportError => !string.IsNullOrEmpty(ExportError);
    public bool HasDraftError => !string.IsNullOrEmpty(DraftError);
    public bool HasRecovery => Recovery is not null;
    public bool HasExternal => External is not null;
    public bool HasFileReadError => !string.IsNullOrEmpty(FileReadError);
    public bool HasNotice => HasSaveError || HasExportError || HasDraftError || HasRecovery || HasExternal || HasFileReadError;
    private void RaiseDerivedState()
    {
        foreach (var property in new[] { nameof(HasSaveError), nameof(HasExportError), nameof(HasDraftError), nameof(HasRecovery), nameof(HasExternal), nameof(HasFileReadError), nameof(HasNotice) })
            this.RaisePropertyChanged(property);
    }
}
