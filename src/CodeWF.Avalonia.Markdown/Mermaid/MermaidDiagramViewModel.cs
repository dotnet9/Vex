using System.ComponentModel;
using Markdig.Syntax;

namespace CodeWF.Avalonia.Markdown.Mermaid;

internal sealed class MermaidDiagramViewModel(string markdown, FencedCodeBlock block, int origin = 0) : INotifyPropertyChanged
{
    private readonly string _originalSource = block.Lines.ToString().TrimEnd('\r', '\n');
    private string _source = block.Lines.ToString().TrimEnd('\r', '\n');
    private bool _isEditing;
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Source
    {
        get => _source;
        set
        {
            if (_source == value) return;
            _source = value;
            PropertyChanged?.Invoke(this, new(nameof(Source)));
        }
    }
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (_isEditing == value) return;
            _isEditing = value;
            PropertyChanged?.Invoke(this, new(nameof(IsEditing)));
        }
    }

    public string PreviewMarkdown => "```mermaid\n" + Source.TrimEnd('\r', '\n') + "\n```";
    public string OriginalMarkdown => markdown;

    public MermaidEditEventArgs? CreateEdit()
    {
        if (Source == _originalSource) return null;
        var bodyStart = markdown.IndexOf('\n', origin + block.Span.Start);
        if (bodyStart < 0) return null;
        var newline = bodyStart > 0 && markdown[bodyStart - 1] == '\r' ? "\r\n" : "\n";
        bodyStart++;
        var hasClosingFence = block.ClosingFencedCharCount > 0;
        var bodyEnd = hasClosingFence
            ? markdown.LastIndexOf('\n', Math.Min(origin + block.Span.End, markdown.Length - 1)) + 1
            : Math.Min(origin + block.Span.End + 1, markdown.Length);
        if (bodyEnd < bodyStart) return null;
        // Markdig 去除了引用/列表前缀；写回时保留这些容器的续行缩进。
        var first = block.Lines.Count > 0 ? origin + block.Lines.Lines[0].Slice.Start : bodyStart;
        var prefix = first >= bodyStart && first <= bodyEnd ? markdown[bodyStart..first] : string.Empty;
        var lines = Source.Replace("\r\n", "\n").TrimEnd('\n').Split('\n');
        var body = string.Join(newline, lines.Select(line => prefix + line));
        if (hasClosingFence) body += newline;
        return new(markdown, markdown[..bodyStart] + body + markdown[bodyEnd..], bodyStart);
    }
}
