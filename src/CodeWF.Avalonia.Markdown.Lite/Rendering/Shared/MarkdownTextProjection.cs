using global::Avalonia.Controls;
using global::Avalonia.Controls.Documents;
using global::Avalonia.VisualTree;

namespace CodeWF.Avalonia.Markdown.Shared.Rendering;

internal readonly record struct MarkdownTextProjection(
    Control Control,
    SelectableTextBlock? TextBlock,
    Run? Run,
    int Start,
    int Length,
    int LocalStart);

internal static class MarkdownTextProjections
{
    public static IEnumerable<MarkdownTextProjection> Create(Control root, string plainText)
    {
        var cursor = new ProjectionCursor();
        var projectedFormulas = new HashSet<Control>();
        foreach (var visual in root.GetVisualDescendants().Prepend(root))
        {
            if (visual is SelectableTextBlock text)
            {
                if (text.Tag as string == "CodeLineNumbers") continue;
                foreach (var projection in ProjectTextBlock(text, plainText, cursor, projectedFormulas))
                {
                    yield return projection;
                }

                continue;
            }

            if (visual is Control control && !projectedFormulas.Contains(control)
                && MarkdownFormulaState.GetText(control) is { Length: > 0 } formulaText
                && plainText.IndexOf(formulaText, cursor.Value, StringComparison.Ordinal) is { } start and >= 0)
            {
                cursor.Value = start + formulaText.Length;
                projectedFormulas.Add(control);
                yield return new MarkdownTextProjection(control, null, null, start, formulaText.Length, 0);
            }
        }
    }

    private static IEnumerable<MarkdownTextProjection> ProjectTextBlock(
        SelectableTextBlock text,
        string plainText,
        ProjectionCursor cursor,
        HashSet<Control> projectedFormulas)
    {
        if (text.Inlines is not { Count: > 0 })
        {
            var buffer = text.Text ?? string.Empty;
            if (buffer.Length > 0 && plainText.IndexOf(buffer, cursor.Value, StringComparison.Ordinal) is { } start and >= 0)
            {
                cursor.Value = start + buffer.Length;
                yield return new MarkdownTextProjection(text, text, null, start, buffer.Length, 0);
            }

            yield break;
        }

        var projections = new List<MarkdownTextProjection>();
        var inlineText = text.Inlines.Text ?? string.Empty;
        var localCursor = 0;

        void Walk(InlineCollection inlines)
        {
            foreach (var inline in inlines)
            {
                switch (inline)
                {
                    case Run run when !string.IsNullOrEmpty(run.Text):
                        var localStart = inlineText.IndexOf(run.Text, localCursor, StringComparison.Ordinal);
                        if (localStart >= 0)
                        {
                            localCursor = localStart + run.Text.Length;
                        }

                        if (plainText.IndexOf(run.Text, cursor.Value, StringComparison.Ordinal) is { } start and >= 0)
                        {
                            cursor.Value = start + run.Text.Length;
                            projections.Add(new MarkdownTextProjection(text, text, run, start, run.Text.Length, Math.Max(0, localStart)));
                        }
                        break;

                    case LineBreak:
                        var localBreak = inlineText.IndexOfAny(['\r', '\n'], localCursor);
                        if (localBreak >= 0)
                        {
                            localCursor = localBreak + (localBreak + 1 < inlineText.Length && inlineText[localBreak] == '\r' && inlineText[localBreak + 1] == '\n' ? 2 : 1);
                        }

                        var documentBreak = plainText.IndexOfAny(['\r', '\n'], cursor.Value);
                        if (documentBreak >= 0)
                        {
                            cursor.Value = documentBreak + (documentBreak + 1 < plainText.Length && plainText[documentBreak] == '\r' && plainText[documentBreak + 1] == '\n' ? 2 : 1);
                        }
                        break;

                    case Span span:
                        Walk(span.Inlines);
                        break;

                    case InlineUIContainer { Child: Control child } when !projectedFormulas.Contains(child)
                                                                      && MarkdownFormulaState.GetText(child) is { Length: > 0 } formulaText:
                        if (plainText.IndexOf(formulaText, cursor.Value, StringComparison.Ordinal) is { } formulaStart and >= 0)
                        {
                            cursor.Value = formulaStart + formulaText.Length;
                            projectedFormulas.Add(child);
                            projections.Add(new MarkdownTextProjection(child, null, null, formulaStart, formulaText.Length, 0));
                        }
                        break;
                }
            }
        }

        Walk(text.Inlines);
        foreach (var projection in projections) yield return projection;
    }

    private sealed class ProjectionCursor
    {
        public int Value { get; set; }
    }
}
