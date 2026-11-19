using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;
using global::Avalonia.Media;

namespace CodeWF.Avalonia.Markdown.Editor.Controls;

/// <summary>Shared selection-style state for source/live editors and host toolbars; color edits commit only on Apply.</summary>
public sealed class MarkdownSpanStylePickerViewModel : INotifyPropertyChanged
{
    private readonly Action<string, string> _apply;
    private readonly Action _clear;
    private readonly Func<string, string?> _query;
    private readonly string[] _swatches;
    private HsvColor _selectedHsv;
    private string _hex = "#0064FA";
    private bool _validColor = true;
    private readonly MarkdownFormattingCommand _applyCommand;

    public MarkdownSpanStylePickerViewModel(string property, Action<string, string> apply, Action clear, Func<string, string?> query)
    {
        if (property is not ("font-size" or "color" or "background-color")) throw new ArgumentOutOfRangeException(nameof(property));
        Property = property; _apply = apply; _clear = clear; _query = query;
        _swatches = [.. (property == "background-color" ? new[] { 0, 1, 2, 3 } : new[] { 4, 5, 6, 7 })
            .SelectMany(shade => MarkdownWysiwygPalette.SpanSemiHues.Select(hue => "#" + hue.Hexes[shade]))];
        SizeCommand = new MarkdownFormattingCommand(value => Commit(Convert.ToDouble(value, CultureInfo.InvariantCulture).ToString("0", CultureInfo.InvariantCulture) + "px"));
        SwatchCommand = new MarkdownFormattingCommand(value => Commit((string)value!));
        _applyCommand = new MarkdownFormattingCommand(_ => Commit(_hex), () => _validColor);
        ClearCommand = new MarkdownFormattingCommand(_ => { _clear(); CloseRequested?.Invoke(this, EventArgs.Empty); });
        Prepare();
    }

    public string Property { get; }
    public bool IsSize => Property == "font-size";
    public bool IsColor => !IsSize;
    public string Title => MarkdownLocalization.GetResource(IsSize ? MarkdownL.SelectionFontSize : Property == "color" ? MarkdownL.SelectionForeground : MarkdownL.SelectionBackground);
    public string ApplyLabel => MarkdownLocalization.GetResource(MarkdownL.SelectionApply);
    public string ClearLabel => MarkdownLocalization.GetResource(MarkdownL.SelectionClear);
    public IReadOnlyList<double> Sizes => MarkdownWysiwygPalette.SpanSizes;
    public IReadOnlyList<string> Swatches => _swatches;
    public ICommand SizeCommand { get; }
    public ICommand SwatchCommand { get; }
    public ICommand ApplyCommand => _applyCommand;
    public ICommand ClearCommand { get; }
    public string? CurrentValue => _query(Property);
    public IBrush PreviewBrush => new SolidColorBrush(_selectedHsv.ToRgb());
    public HsvColor SelectedHsv
    {
        get => _selectedHsv;
        set
        {
            if (_selectedHsv == value) return;
            _selectedHsv = value;
            var rgb = value.ToRgb();
            _hex = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}"; _validColor = true;
            Changed();
        }
    }
    public string HexValue
    {
        get => _hex;
        set
        {
            _hex = value ?? string.Empty;
            _validColor = _hex.Length == 7 && _hex[0] == '#' && Color.TryParse(_hex, out _);
            if (_validColor) _selectedHsv = Color.Parse(_hex).ToHsv();
            Changed();
        }
    }

    /// <summary>Refresh immediately before opening; opening/canceling never writes the document.</summary>
    public void Prepare()
    {
        if (!IsSize)
        {
            var current = _query(Property);
            _selectedHsv = (Color.TryParse(current, out var color) ? color : Color.Parse(Property == "color" ? "#0064FA" : "#FFF3BF")).ToHsv();
            var rgb = _selectedHsv.ToRgb(); _hex = $"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}"; _validColor = true;
        }
        Changed();
    }

    private void Commit(string value)
    {
        // Confirming the current color must not toggle it off through ExecuteSpan.
        if (!string.Equals(_query(Property), value, StringComparison.OrdinalIgnoreCase)) _apply(Property, value);
        CloseRequested?.Invoke(this, EventArgs.Empty);
    }
    private void Changed() { PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null)); _applyCommand?.Refresh(); }
    public event PropertyChangedEventHandler? PropertyChanged;
    public event EventHandler? CloseRequested;
}
