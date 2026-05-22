using System.Runtime.CompilerServices;
using System.Text;
using ReactiveUI;
using Vex.Core.Models;

namespace Vex.Modules.Shell.ViewModels;

// 保存当前文档的派生展示信息；文件读写流程仍由 MainWindowViewModel 统一协调。
public sealed class ShellDocumentInfoViewModel : ReactiveObject
{
    private DocumentSnapshot _document = new(null, "Untitled.md", string.Empty, Encoding.UTF8, true);
    private string _markdown = string.Empty;
    private string _lastSavedMarkdown = string.Empty;
    private MarkdownStatistics _statistics = new(0, 0, 1);

    public string WindowTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName} - Vex";

    public string CurrentDocumentTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName}";

    public string? CurrentFilePath => _document.FilePath;

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public bool IsModified => !string.Equals(_markdown, _lastSavedMarkdown, StringComparison.Ordinal);

    public string DocumentStateText => IsModified ? "Modified" : "Saved";

    public string CurrentEncodingText => GetEncodingDisplayName(_document.Encoding);

    public MarkdownStatistics Statistics
    {
        get => _statistics;
        private set
        {
            if (SetProperty(ref _statistics, value))
            {
                OnPropertyChanged(nameof(WordCountText));
                OnPropertyChanged(nameof(CharacterCountText));
                OnPropertyChanged(nameof(LineCountText));
            }
        }
    }

    public string WordCountText => $"{Statistics.Words} words";

    public string CharacterCountText => $"{Statistics.Characters} chars";

    public string LineCountText => $"{Statistics.Lines} lines";

    public string PropertyNameText => _document.FileName;

    public string PropertyLocationText => CurrentFilePath ?? "Unsaved document";

    public string PropertySizeText => CurrentFilePath is { Length: > 0 } path && File.Exists(path)
        ? FormatFileSize(new FileInfo(path).Length)
        : $"{Encoding.UTF8.GetByteCount(_markdown):N0} B";

    public void Refresh(
        DocumentSnapshot document,
        string markdown,
        string lastSavedMarkdown,
        MarkdownStatistics statistics)
    {
        // 刷新时一次性广播所有派生属性，避免调用方遗漏标题、属性面板或状态栏字段。
        _document = document;
        _markdown = markdown;
        _lastSavedMarkdown = lastSavedMarkdown;
        Statistics = statistics;
        OnPropertyChanged(nameof(WindowTitle));
        OnPropertyChanged(nameof(CurrentDocumentTitle));
        OnPropertyChanged(nameof(CurrentFilePath));
        OnPropertyChanged(nameof(HasCurrentFile));
        OnPropertyChanged(nameof(IsModified));
        OnPropertyChanged(nameof(DocumentStateText));
        OnPropertyChanged(nameof(CurrentEncodingText));
        OnPropertyChanged(nameof(PropertyNameText));
        OnPropertyChanged(nameof(PropertyLocationText));
        OnPropertyChanged(nameof(PropertySizeText));
    }

    private static string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes:N0} B",
            < 1024 * 1024 => $"{bytes / 1024d:N1} KB",
            _ => $"{bytes / 1024d / 1024d:N1} MB"
        };
    }

    private static string GetEncodingDisplayName(Encoding encoding)
    {
        if (encoding is UTF8Encoding { Preamble.Length: > 0 })
        {
            return "UTF-8 BOM";
        }

        if (encoding.CodePage == Encoding.UTF8.CodePage)
        {
            return "UTF-8";
        }

        return encoding.WebName.ToUpperInvariant();
    }

    private bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(storage, value))
        {
            return false;
        }

        this.RaiseAndSetIfChanged(ref storage, value, propertyName);
        return true;
    }

    private void OnPropertyChanged(string propertyName)
    {
        this.RaisePropertyChanged(propertyName);
    }
}
