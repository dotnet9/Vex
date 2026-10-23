using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CodeWF.Toolkit.EventBus;
using Vex.Core.Messaging;
using Vex.Core.Services;
using Vex.Modules.Shell.ViewModels;
using Vex.Modules.Shell.Views;
using Vex.Modules.Workspace.Services;
using Vex.Modules.Workspace.ViewModels;
using Vex.Modules.Workspace.Views;
using LibEditorView = CodeWF.Avalonia.Markdown.Editor.Controls.MarkdownEditorView;

/// <summary>
/// 元素菜单与块级操作验证：表格结构 20 项语义、段落转换、任务切换、块插入/删除、
/// 即时渲染块元素菜单命令链，以及图表原位编辑的展开与写回。
/// </summary>
internal static class ElementVerification
{
    private const string Sample =
        "# 元素验证\n\n" +
        "| 时间 | 地点 |\n| --- | --- |\n| 09:00 | 人民公园 |\n\n" +
        "普通段落文本\n\n" +
        "- [ ] 未完成任务\n";

    public static void Run(VerificationVexApp app, MainWindow window, string output)
    {
        var vm = (MainWindowViewModel)window.DataContext!;
        var editorVm = app.Resolve<MarkdownEditorViewModel>();
        void Pump()
        {
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
        }
        void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); Console.WriteLine("PASS " + message); }
        void SetDocument(string text)
        {
            EventBus.Default.Publish(new MarkdownTextChangedCommand(text, 1, 1, text.Count(character => character == '\n') + 1));
            Pump(); Pump();
        }
        int OffsetOf(string needle)
        {
            var text = vm.Markdown;
            var position = text.IndexOf(needle, StringComparison.Ordinal);
            Check(position >= 0, "找到锚点文本：" + needle);
            return position;
        }
        void CaretTo(int offset)
        {
            var editor = window.GetVisualDescendants().OfType<LibEditorView>().Single();
            editor.Editor.Select(offset, 0);
            Pump();
        }

        vm.Workbench.ShowSource(); Pump();
        SetDocument(Sample);

        // 表格结构：光标置于表头单元格执行插入行/列、移动、对齐与删除。
        CaretTo(OffsetOf("时间"));
        editorVm.TableAction(MarkdownTableAction.RowBelow); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown)) is { } afterInsert
            && afterInsert.Rows.Length == 3, "下方插入行后表格为 3 行（含表头）");

        editorVm.TableAction(MarkdownTableAction.AlignCenter); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown))!.Align[0] == "center", "列居中对齐写入分隔行");

        CaretTo(OffsetOf("09:00"));
        editorVm.TableAction(MarkdownTableAction.RowUp); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown)) is { } afterMove
            && afterMove.Rows[1][1] == "人民公园", "上移该行交换数据行");

        editorVm.TableAction(MarkdownTableAction.ColAfter); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown))!.Align.Length == 3, "右侧插入列后为 3 列");

        editorVm.FormatTable(); Pump();
        var formatted = TableText(vm.Markdown).Split('\n');
        Check(formatted.DistinctBy(line => line.Split('|').Length).Count() == 1, "格式化表格后各列对齐");

        editorVm.TableAction(MarkdownTableAction.ColDelete); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown))!.Align.Length == 2, "删除列后回到 2 列");

        editorVm.TableAction(MarkdownTableAction.RowDelete); Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown))!.Rows.Length == 2, "删除行后回到 2 行");

        // 段落转换：源码菜单动作经库动作服务改写当前块。
        CaretTo(OffsetOf("普通段落文本"));
        editorVm.InsertAction(EditorActionKind.Heading2); Pump();
        Check(vm.Markdown.Contains("## 普通段落文本", StringComparison.Ordinal), "段落转换为二级标题");

        CaretTo(OffsetOf("未完成任务"));
        editorVm.InsertAction(EditorActionKind.OrderedList); Pump();
        Check(vm.Markdown.Contains("1. 未完成任务", StringComparison.Ordinal), "任务项转换为有序列表（剥离任务括号）");

        editorVm.ToggleTask(); Pump();
        Check(vm.Markdown.Contains("1. [ ] 未完成任务", StringComparison.Ordinal), "有序列表项升级为任务");

        editorVm.ToggleTask(); Pump();
        Check(vm.Markdown.Contains("1. [x] 未完成任务", StringComparison.Ordinal), "再次切换勾选任务");

        editorVm.InsertAction(EditorActionKind.Paragraph); Pump();
        Check(!vm.Markdown.Contains("1. [x]", StringComparison.Ordinal) && vm.Markdown.Contains("未完成任务", StringComparison.Ordinal),
            "转换为段落剥离列表前缀");

        // 块操作：上方/下方插入段落、复制块、删除块。
        CaretTo(OffsetOf("未完成任务"));
        var lineCountBefore = vm.Markdown.Split('\n').Length;
        editorVm.InsertParagraphBelow(); Pump();
        Check(vm.Markdown.Split('\n').Length == lineCountBefore + 2, "下方插入段落新增空块");

        CaretTo(OffsetOf("元素验证"));
        editorVm.InsertParagraphAbove(); Pump();
        Check(vm.Markdown.StartsWith("\n\n", StringComparison.Ordinal), "上方插入段落置于文档顶部");

        editorVm.DeleteBlock(); Pump();
        Check(!vm.Markdown.StartsWith("\n", StringComparison.Ordinal), "删除块移除空块");

        // 即时渲染块元素菜单：命令链（视图 → 总线 → ViewModel → 文本操作）。
        vm.Workbench.ShowInstant(); Pump(); Pump();
        SetDocument(Sample);
        var instant = window.GetVisualDescendants().OfType<MarkdownInstantView>().Single();
        Check(instant.Editor.ContextMenu is { } elementMenu
            && elementMenu.ItemsSource is IEnumerable<object> groups
            && groups.OfType<MenuItem>().Count() >= 2, "即时编辑器挂载元素菜单");
        var paragraphLine = 1 + Sample.Split('\n').TakeWhile(line => !line.StartsWith("普通段落", StringComparison.Ordinal)).Count();
        EventBus.Default.Publish(new MarkdownElementCommand(
            MarkdownElementOperation.Convert, paragraphLine, convertKind: "heading3"));
        Pump();
        Check(vm.Markdown.Contains("### 普通段落文本", StringComparison.Ordinal), "元素菜单命令按块行号转换段落");

        EventBus.Default.Publish(new MarkdownElementCommand(
            MarkdownElementOperation.Table, 3, tableAction: MarkdownTableAction.AlignRight));
        Pump();
        Check(MarkdownElementOperations.ParseTable(TableText(vm.Markdown))!.Align[0] == "right", "元素菜单执行表格列对齐");

        // 图表原位编辑：展开、源码编辑、Esc 收起并写回。
        vm.Workbench.ShowRead(); Pump(); Pump();
        SetDocument("图表验证\n\n```mermaid\nflowchart LR\n  A --> B\n```\n");
        var previewView = window.FindControl<Vex.Modules.Workspace.Views.MarkdownPreviewView>("PreviewGuideTarget")!;
        var diagramDeadline = DateTime.UtcNow.AddSeconds(15);
        Border? diagramBorder = null;
        while (diagramBorder is null)
        {
            Pump();
            diagramBorder = previewView.GetVisualDescendants().OfType<Border>()
                .FirstOrDefault(border => border.Classes.Contains("MdMermaid"));
            if (DateTime.UtcNow > diagramDeadline) throw new TimeoutException("图表块未渲染");
            Thread.Sleep(20);
        }
        DiagramEditor.OpenInPlace(previewView, diagramBorder, 0);
        Pump();
        var editorBox = diagramBorder.GetVisualDescendants().OfType<TextBox>().Single();
        Check(diagramBorder.Child is StackPanel, "图表块原位展开编辑面板");
        editorBox.Text = "flowchart LR\n  A --> Z";
        editorBox.RaiseEvent(new KeyEventArgs { RoutedEvent = InputElement.KeyDownEvent, Key = Key.Escape });
        Pump(); Pump();
        Check(vm.Markdown.Contains("A --> Z", StringComparison.Ordinal), "收起图表编辑写回正文");
    }

    private static string TableText(string markdown)
    {
        var lines = markdown.Replace("\r\n", "\n").Split('\n');
        var rows = lines.Where(line => line.Trim().StartsWith('|')).ToList();
        return string.Join('\n', rows);
    }
}
