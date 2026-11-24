using CodeWF.Avalonia.Markdown.Core.Blocks;
using CodeWF.Avalonia.Markdown.Editor.Controls;
using CodeWF.Avalonia.Markdown.Editor.Controls.Wysiwyg;

using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;

using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

[Collection("AvaloniaPlatform")]
public sealed class MarkdownTableEditingTests(AvaloniaPlatformFixture platform)
{
    [Fact]
    public void ShiftEnter_InsertsHtmlLineBreakInCell()
    {
        platform.Run(() =>
        {
            var table = CreateTable(out var window);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var cell = table.FirstCell!;
                cell.BeginEditAtEnd();
                RaiseKey(cell, Key.Enter, KeyModifiers.Shift);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("甲<br>", cell.EditingInput.Text);
                Assert.Equal("甲<br>", table.Model.Cells[0][0]);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void TabAndShiftTab_MoveAcrossGrid()
    {
        platform.Run(() =>
        {
            var table = CreateTable(out var window);
            try
            {
                Dispatcher.UIThread.RunJobs();
                var cells = GetCells(table);
                cells[0][0].BeginEditAtEnd();

                RaiseKey(cells[0][0], Key.Tab, KeyModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(cells[0][1], table.ActiveCell);

                RaiseKey(cells[0][1], Key.Tab, KeyModifiers.Shift);
                Dispatcher.UIThread.RunJobs();
                Assert.Same(cells[0][0], table.ActiveCell);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Fact]
    public void Enter_MovesDownSameColumnAndAddsRowAtEnd()
    {
        platform.Run(() =>
        {
            var table = CreateTable(out var window);
            table.StructureChangeRequested = (view, action) =>
            {
                if (MarkdownTableEditor.ChangeStructure(view.Model, action)) view.RefreshFromModel();
            };
            try
            {
                Dispatcher.UIThread.RunJobs();
                var cells = GetCells(table);
                cells[0][1].BeginEditAtEnd();

                RaiseKey(cells[0][1], Key.Enter, KeyModifiers.None);
                Dispatcher.UIThread.RunJobs();
                cells = GetCells(table);
                Assert.Same(cells[1][1], table.ActiveCell);

                RaiseKey(cells[1][1], Key.Enter, KeyModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(3, table.Model.Cells.Count);
                Assert.Same(GetCells(table)[2][1], table.ActiveCell);
            }
            finally
            {
                window.Close();
            }
        });
    }

    [Theory]
    [InlineData("甲\n乙", "甲<br>乙")]
    [InlineData("甲\r\n乙\r丙", "甲<br>乙<br>丙")]
    [InlineData("甲|乙", "甲|乙")]
    public void Normalize_ConvertsBareNewlinesOnly(string input, string expected) =>
        Assert.Equal(expected, MarkdownTableCellText.Normalize(input));

    [Fact]
    public void Serialize_NormalizesCellNewlinesBeforeEscapingPipes()
    {
        var model = new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.Table,
            HasHeader = true,
            Cells = [["甲\n乙|丙"]],
        };

        var text = MarkdownBlockParser.Write([model]);

        Assert.Contains("甲<br>乙\\|丙", text);
        Assert.DoesNotContain("甲\n乙", text);
    }

    [Fact]
    public void LiveSelection_MapsActiveCellToTableSource()
    {
        platform.Run(() =>
        {
            var view = new MarkdownLiveEditorView();
            var window = new Window { Width = 600, Height = 300, Content = view };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                view.SetText("| H1 | H2 |\n| --- | --- |\n| a | b |");
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                view.FocusBlock(0);
                var table = view.GetVisualDescendants().OfType<MarkdownTableView>().Single();
                table.FirstCell!.BeginEditAtEnd();
                RaiseKey(table.FirstCell, Key.Tab, KeyModifiers.None);
                var header = table.ActiveCell!.EditingInput;
                header.CaretIndex = 1;
                header.SelectionStart = 0;
                header.SelectionEnd = 1;
                Assert.Equal((view.Text.IndexOf("H2", StringComparison.Ordinal), 1), view.Selection);
                Assert.Equal("H", view.SelectedText);

                RaiseKey(table.ActiveCell, Key.Enter, KeyModifiers.None);
                Assert.Equal((1, 1), table.ActiveCellPosition);
                var body = table.ActiveCell!.EditingInput;
                body.CaretIndex = 1;
                body.SelectionStart = 0;
                body.SelectionEnd = 1;
                Assert.Equal((view.Text.IndexOf('b'), 1), view.Selection);
                Assert.Equal("b", view.SelectedText);
            }
            finally
            {
                window.Close();
            }
        });
    }

    private static MarkdownTableView CreateTable(out Window window)
    {
        var model = new MarkdownBlockModel
        {
            Kind = MarkdownBlockKind.Table,
            HasHeader = true,
            Cells = [["甲", "乙"], ["丙", "丁"]],
        };
        var table = new MarkdownTableView(model, new MarkdownBlockViewOptions());
        window = new Window { Width = 600, Height = 300, Content = table };
        window.Show();
        return table;
    }

    private static List<List<MarkdownBlockView>> GetCells(MarkdownTableView table) =>
        (List<List<MarkdownBlockView>>)typeof(MarkdownTableView)
            .GetField("_cells", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
            .GetValue(table)!;

    private static void RaiseKey(MarkdownBlockView cell, Key key, KeyModifiers modifiers)
    {
        var args = new KeyEventArgs
        {
            RoutedEvent = InputElement.KeyDownEvent,
            Source = cell.EditingInput,
            Key = key,
            KeyModifiers = modifiers,
        };
        cell.EditingInput.RaiseEvent(args);
        Assert.True(args.Handled);
    }
}
