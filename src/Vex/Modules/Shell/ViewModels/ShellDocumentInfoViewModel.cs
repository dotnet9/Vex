using System.Runtime.CompilerServices;
using System.Text;
using ReactiveUI;
using Vex.Core.Models;
using Vex.Core.Services;

namespace Vex.Modules.Shell.ViewModels;

// 保存当前文档的派生展示信息；文件读写流程仍由 MainWindowViewModel 统一协调。
public sealed class ShellDocumentInfoViewModel : ReactiveObject
{
    private readonly IAppLocalizer _localizer;
    private DocumentSnapshot _document;
    private string _markdown = string.Empty;
    private string _lastSavedMarkdown = string.Empty;
    private MarkdownStatistics _statistics = new(0, 0, 1);

    public ShellDocumentInfoViewModel(IAppLocalizer localizer)
    {
        _localizer = localizer;
        _document = new(null, _localizer.Get(VexL.DocumentDefaultFileName), string.Empty, Encoding.UTF8, true);
        _localizer.CultureChanged += OnCultureChanged;
    }

    public string WindowTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName} - Vex";

    public string CurrentDocumentTitle => $"{(IsModified ? "*" : string.Empty)}{_document.FileName}";

    public string? CurrentFilePath => _document.FilePath;

    public bool HasCurrentFile => !string.IsNullOrWhiteSpace(CurrentFilePath);

    public bool IsModified => !MarkdownTextComparer.EqualsNormalizedLineEndings(_markdown, _lastSavedMarkdown);

    public string DocumentStateText => IsModified
        ? _localizer.Get(VexL.DocumentStateModified)
        : _localizer.Get(VexL.DocumentStateSaved);

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
                OnPropertyChanged(nameof(ReadingMinutesText));
            }
        }
    }

    public string WordCountText => _localizer.Format(VexL.WordCountFormat, Statistics.Words);

    public string CharacterCountText => _localizer.Format(VexL.CharacterCountFormat, Statistics.Characters);

    public string LineCountText => _localizer.Format(VexL.LineCountFormat, Statistics.Lines);

    public string ReadingMinutesText => _localizer.Format(VexL.ReadingMinutesFormat, Statistics.ReadingMinutes);

    public string PropertyNameText => _document.FileName;

    public string PropertyLocationText => CurrentFilePath ?? _localizer.Get(VexL.UnsavedDocument);

    public string PropertySizeText => CurrentFilePath is { Length: > 0 } path && File.Exists(path)
        ? FormatFileSize(new FileInfo(path).Length)
        : $"{Encoding.UTF8.GetByteCount(_markdown).ToString("N0", _localizer.Culture)} B";

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

    private void OnCultureChanged(object? sender, EventArgs e)
    {
        // 这些字段是从当前文档派生出来的展示文案，语言切换时必须主动通知绑定刷新。
        OnPropertyChanged(nameof(DocumentStateText));
        OnPropertyChanged(nameof(WordCountText));
        OnPropertyChanged(nameof(CharacterCountText));
        OnPropertyChanged(nameof(LineCountText));
        OnPropertyChanged(nameof(ReadingMinutesText));
        OnPropertyChanged(nameof(PropertyLocationText));
    }

    private string FormatFileSize(long bytes)
    {
        return bytes switch
        {
            < 1024 => $"{bytes.ToString("N0", _localizer.Culture)} B",
            < 1024 * 1024 => $"{(bytes / 1024d).ToString("N1", _localizer.Culture)} KB",
            _ => $"{(bytes / 1024d / 1024d).ToString("N1", _localizer.Culture)} MB"
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
