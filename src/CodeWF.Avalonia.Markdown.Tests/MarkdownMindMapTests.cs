using CodeWF.Avalonia.MindView;
using CodeWF.Avalonia.MindView.Controls;
using CodeWF.Avalonia.MindView.Markdown;
using System.Collections.ObjectModel;
using global::Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.Media;
using global::Avalonia.Threading;
using global::Avalonia.VisualTree;
using Xunit;

namespace CodeWF.Avalonia.Markdown.Tests;

public sealed class MarkdownMindMapDocumentTests
{
    [Fact]
    public void RenameKeepsHeadingIdentityAndSelection()
    {
        const string before = "# One\n\n## Same\n\n## Same\n";
        const string after = "# One\n\n## Renamed\n\n## Same\n";
        var oldRoot = Parse(before);
        var newRoot = Parse(after);
        var selected = oldRoot.Children[0];

        var match = MarkdownMindMapDocument.FindCorrespondingNode(oldRoot, newRoot, selected);

        Assert.NotNull(match);
        Assert.Equal("Renamed", match.Title);
        Assert.Equal(selected.Id, match.Id);
        Assert.NotEqual(oldRoot.Children[0].Id, oldRoot.Children[1].Id);
    }

    [Fact]
    public void AlertsAreIndependentSemanticNodes()
    {
        var root = Parse("# One\n\n> [!WARNING]\n> Be **careful**.\n\n> Ordinary quote\n");
        var alert = Assert.Single(root.Children, node => node.Kind == MarkdownMindMapNodeKind.Alert);

        Assert.Equal("warning", alert.AlertKind);
        Assert.Equal("Be careful.", alert.FullText);
        Assert.DoesNotContain("[!WARNING]", alert.FullText);
        Assert.Contains(root.Children, node => node.Kind == MarkdownMindMapNodeKind.Quote);
    }

    [Fact]
    public void CollapseStateFollowsStructureWhenInsertionShiftsOffsets()
    {
        var oldRoot = Parse("# One\n\n## Target\n\n### Child\n");
        var target = oldRoot.Children[0];
        target.IsCollapsed = true;
        var newRoot = Parse("# One\n\nIntro\n\n## Target\n\n### Child\n");

        MarkdownMindMapDocument.CopyCollapsedState(oldRoot, newRoot);
        var match = MarkdownMindMapDocument.FindCorrespondingNode(oldRoot, newRoot, target);

        Assert.NotNull(match);
        Assert.True(match.IsCollapsed);
    }

    private static MarkdownMindMapNode Parse(string markdown) => MarkdownMindMapDocument.Parse(
        markdown,
        options: new MarkdownMindMapOptions { ShowParagraphs = true });
}

[Collection("AvaloniaPlatform")]
public sealed class MarkdownMindMapLayoutTests(AvaloniaPlatformFixture platform)
{
    private const string Document = """
        # Root
        ## Branch A
        ### Child A1
        #### Deep A1a
        ##### Deep A1b
        ###### Deep A1c
        ####### Deep A1d
        ######## Deep A1e
        ######### Deep A1f
        ### Child A2
        ## Branch B
        ### Child B1
        ## Branch C
        ### Child C1
        """;

    [Fact]
    public void AllLayoutsSurviveCollapsedBranchesAndCollapsedRoot()
    {
        platform.Run(() =>
        {
            var map = CreateMap();
            var window = new Window { Width = 1100, Height = 760, Content = map };
            window.Show();
            try
            {
                foreach (var layout in Enum.GetValues<MindMapLayoutKind>())
                {
                    map.LayoutKind = layout;
                    map.ExpandAll();
                    map.CollapseAll();
                    Assert.NotNull(map.GetNodeBounds(map.Root!.Id));

                    map.Root.IsCollapsed = true;
                    map.ClearSelection();
                    Assert.Single(Flatten(map.Root), node => map.GetNodeBounds(node.Id) is not null);
                }
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void OutlineKeepsDeepCardsPositiveAndOrdered()
    {
        platform.Run(() =>
        {
            var map = CreateMap();
            map.LayoutKind = MindMapLayoutKind.Outline;
            var window = new Window { Width = 1100, Height = 760, Content = map };
            window.Show();
            try
            {
                var nodes = Flatten(map.Root!).Where(node => map.GetNodeBounds(node.Id) is { }).ToArray();
                Assert.All(nodes, node => Assert.True(map.GetNodeBounds(node.Id)!.Value.Width > 0));
                for (var index = 1; index < nodes.Length; index++)
                    Assert.True(map.GetNodeBounds(nodes[index - 1].Id)!.Value.Bottom
                        <= map.GetNodeBounds(nodes[index].Id)!.Value.Top);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void FishbonePlacesRootAtSpineEndAndAlternatesBranches()
    {
        platform.Run(() =>
        {
            var map = CreateMap();
            map.LayoutKind = MindMapLayoutKind.Fishbone;
            var window = new Window { Width = 1100, Height = 760, Content = map };
            window.Show();
            try
            {
                var root = map.GetNodeBounds(map.Root!.Id)!.Value;
                var branches = map.Root.Children.Select(node => map.GetNodeBounds(node.Id)!.Value).ToArray();
                Assert.All(branches, branch => Assert.True(branch.Left < root.Left));
                Assert.True(branches[0].Center.Y < root.Center.Y);
                Assert.True(branches[1].Center.Y > root.Center.Y);
                Assert.True(branches[2].Center.Y < root.Center.Y);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void AlertCardsUseSemanticBorders()
    {
        platform.Run(() =>
        {
            var map = new MarkdownMindMapView
            {
                EnableMotion = false,
                SuccessColor = Colors.LimeGreen,
                WarningColor = Colors.Orange,
                DangerColor = Colors.Red,
                Root = MarkdownMindMapDocument.Parse("# Root\n\n> [!NOTE]\n> Note\n\n> [!TIP]\n> Tip\n\n> [!WARNING]\n> Warning\n\n> [!CAUTION]\n> Caution\n")
            };
            var window = new Window { Width = 1100, Height = 760, Content = map };
            window.Show();
            try
            {
                var borders = map.GetVisualDescendants().OfType<Border>()
                    .Where(border => border.Width == 224 && border.BorderThickness == new Thickness(2))
                    .Select(border => (border.BorderBrush as ISolidColorBrush)?.Color)
                    .OfType<Color>().ToArray();
                Assert.Contains(Colors.LimeGreen, borders);
                Assert.Contains(Colors.Orange, borders);
                Assert.Contains(Colors.Red, borders);
                Assert.Contains(map.AccentColor, borders);
            }
            finally { window.Close(); }
        });
    }

    [Fact]
    public void ChapterKeyboardShortcutsPreserveHierarchyAndMoveIntents()
    {
        platform.Run(() =>
        {
            var map = CreateMap();
            map.IsChapterEditingEnabled = true;
            var window = new Window { Width = 1100, Height = 760, Content = map };
            var actions = new List<string>();
            map.ChapterActionRequested += (_, action) => actions.Add(action.Action);
            window.Show();
            try
            {
                var branch = map.Root!.Children[0];
                map.SelectNode(branch.Id);
                Press(map, Key.Tab, KeyModifiers.Shift);
                Press(map, Key.Up, KeyModifiers.Alt);
                Press(map, Key.Down, KeyModifiers.Alt);
                Press(map, Key.F2, KeyModifiers.None);
                Assert.Equal(new[] { "promote", "move-up", "move-down", "rename" }, actions);
            }
            finally { window.Close(); }

            static void Press(MarkdownMindMapView map, Key key, KeyModifiers modifiers)
            {
                map.RaiseEvent(new KeyEventArgs
                {
                    RoutedEvent = InputElement.KeyDownEvent,
                    Key = key,
                    KeyModifiers = modifiers
                });
            }
        });
    }

    private static MarkdownMindMapView CreateMap()
    {
        var map = new MarkdownMindMapView
        {
            EnableMotion = false,
            Root = MarkdownMindMapDocument.Parse(Document)
        };
        return map;
    }

    private static IEnumerable<MarkdownMindMapNode> Flatten(MarkdownMindMapNode node)
    {
        yield return node;
        foreach (var child in node.Children)
        foreach (var descendant in Flatten(child))
            yield return descendant;
    }
}

[Collection("AvaloniaPlatform")]
public sealed class MindMapEditorReflowTests(AvaloniaPlatformFixture platform)
{
    [Theory]
    [InlineData("Title")]
    [InlineData("Note")]
    public void TextChangesReflowDescendantGeometry(string property)
    {
        platform.Run(() =>
        {
            var root = new MindMapNode("Root");
            var child = new MindMapNode("Child");
            root.Children.Add(child);
            var editor = new MindMapEditor
            {
                Roots = new ObservableCollection<MindMapNode> { root },
                LayoutKind = MindMapLayoutKind.Right
            };
            var window = new Window { Width = 900, Height = 600, Content = editor };
            window.Show();
            try
            {
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                editor.ArrangeNodes();
                var before = child.X;
                if (property == "Title") root.Title = new string('长', 80);
                else root.Note = new string('长', 80);
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                Assert.True(child.X > before);
            }
            finally { window.Close(); }
        });
    }
}
