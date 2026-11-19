using Avalonia;
using global::Avalonia.Controls;
using global::Avalonia.Input;
using global::Avalonia.Interactivity;
using global::Avalonia.VisualTree;
using CodeWF.Avalonia.Markdown.Rendering;
using CodeWF.Avalonia.Markdown.Shared.Rendering;

namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// MarkdownViewer 的能力 API 部分：后台解析调度、阅读位置存取、远程图片刷新与
/// 任务列表勾选回写。宿主通过这些公共 API 替代自行实现（反射探测滚动位置、
/// URL 追参强制刷新、源码正则改写等）。
/// </summary>
public partial class MarkdownViewer
{
    /// <summary>阅读宿主可关闭任务勾选，避免查看文档时产生编辑行为。</summary>
    public static readonly StyledProperty<bool> IsTaskEditingEnabledProperty =
        AvaloniaProperty.Register<MarkdownViewer, bool>(nameof(IsTaskEditingEnabled), true);
    public bool IsTaskEditingEnabled { get => GetValue(IsTaskEditingEnabledProperty); set => SetValue(IsTaskEditingEnabledProperty, value); }
    // 渲染调度：文本变更只登记快照，合并窗口后在后台线程解析，过期结果丢弃。
    private readonly MarkdownRenderScheduler _scheduler;
    private MarkdownRenderMode _lastRenderMode = MarkdownRenderMode.Full;
    private TimeSpan _lastParseDuration;
    private int _taskMarkerOffset = -1;

    /// <summary>
    /// 指定块控件当前是否已物化（虚拟化宿主下未进入视口的块不挂载控件）；
    /// 宿主可用它区分「未物化」与「不存在」。
    /// </summary>
    public bool IsBlockRealized(int blockIndex) =>
        _virtualizingHost is null || _virtualizingHost.GetRealizedItem(blockIndex) is not null;

    /// <summary>
    /// 已物化的块数量；未启用虚拟化时等于块总数，供诊断与测试使用。
    /// </summary>
    public int RealizedBlockCount => _virtualizingHost?.RealizedItemCount ?? _renderedBlocks.Count;

    /// <summary>
    /// 阅读位置快照：已渲染块序号与相对进度（正文内 0-1，块前留白允许负值）。
    /// 块序号在文档重排后仍可定位，避免用像素值导致跳位。
    /// </summary>
    public readonly record struct MarkdownReadingPosition(int BlockIndex, double BlockRatio, double DocumentRatio);

    /// <summary>
    /// 服务该 Viewer 的滚动宿主；未显式指定时自动向上查找 <see cref="ScrollViewer"/>。
    /// </summary>
    public ScrollViewer? ScrollHost { get; set; }

    /// <summary>
    /// 任务列表勾选回写事件：宿主将 <see cref="MarkdownTaskWriteResult.Markdown"/>
    /// 按变更区间应用到编辑器，并保持撤销栈。
    /// </summary>
    public event EventHandler<MarkdownTaskWriteResult>? TaskWriteBackRequested;

    /// <summary>
    /// 最近一次渲染采用的模式（增量 / 全量），供宿主展示与诊断。
    /// </summary>
    public MarkdownRenderMode LastRenderMode => _lastRenderMode;

    /// <summary>
    /// 最近一次解析耗时，供宿主状态栏与性能面板展示。
    /// </summary>
    public TimeSpan LastParseDuration => _lastParseDuration;

    /// <summary>
    /// 基于当前 Markdown 的文档模型（含大纲、统计等增量解析入口）；
    /// 后台解析结果尚未到达时返回最近一次渲染使用的模型。
    /// </summary>
    public MarkdownDocumentModel CurrentModel => _renderedModel;

    /// <summary>
    /// 采集当前阅读位置；文档未渲染或滚动宿主不可用时返回 null。
    /// </summary>
    public MarkdownReadingPosition? SaveReadingPosition()
    {
        if (_renderedBlocks.Count == 0 || _documentHost is null)
        {
            return null;
        }

        var host = ResolveScrollHost();
        if (host is null)
        {
            return new MarkdownReadingPosition(0, 0, 0);
        }

        var offset = host.Offset.Y;
        var viewportHeight = Math.Max(1, host.Viewport.Height);
        var documentHeight = Math.Max(viewportHeight, host.Extent.Height);
        // 视口中心换算到文档坐标，兼容宿主留白及演示缩放。
        var probeOffset = host.TranslatePoint(new Point(0, viewportHeight / 2), _documentHost)?.Y ?? offset + viewportHeight / 2;

        for (var i = 0; i < _renderedBlocks.Count; i++)
        {
            // 未物化/未量测的块拿不到可信位置：跳过昂贵的 TranslatePoint，
            // 虚拟化下每帧只需检查视口附近那几十个块（这个采样在滚动中会反复执行）。
            if (_virtualizingHost is not null && !_virtualizingHost.IsItemMeasured(i))
            {
                continue;
            }

            var control = _renderedBlocks[i].Control;
            if (control.TranslatePoint(new Point(0, 0), _documentHost) is not { } origin)
            {
                continue;
            }

            // origin 与 probeOffset 都在文档坐标中；不能再减一次滚动偏移。
            var top = origin.Y;
            var height = Math.Max(1, control.Bounds.Height);
            if (probeOffset > top + height)
            {
                continue;
            }

            // 段间留白归到下一块；保留负比例，否则往返恢复会吸附到段首并丢失像素位置。
            var ratioInBlock = (probeOffset - top) / height;
            var documentRatio = Math.Clamp((offset + viewportHeight / 2) / documentHeight, 0d, 1d);
            return new MarkdownReadingPosition(i, ratioInBlock, documentRatio);
        }

        var lastIndex = _renderedBlocks.Count - 1;
        return new MarkdownReadingPosition(lastIndex, 1, Math.Clamp((offset + viewportHeight / 2) / documentHeight, 0d, 1d));
    }

    /// <summary>
    /// 恢复到指定阅读位置；定位块尚未布局时返回 false，宿主可在布局完成后重试。
    /// </summary>
    public bool RestoreReadingPosition(MarkdownReadingPosition? position)
    {
        if (position is not { } value || _renderedBlocks.Count == 0 || _documentHost is null)
        {
            return false;
        }

        var host = ResolveScrollHost();
        if (host is null || host.Extent.Height <= 0)
        {
            return false;
        }

        var index = Math.Clamp(value.BlockIndex, 0, _renderedBlocks.Count - 1);
        // 定位完成前固定目标，避免布局按旧视口立即回收；滚动后由现有锚点策略管理。
        _virtualizingHost?.PinItem(index);
        _virtualizingHost?.RealizeItem(index);
        // 恢复全文时前面的离屏块仍用估高。先滚到锚点，再量测新视口，
        // 按同一块内位置校正；只设一次像素偏移会在新块量测后落到相邻章节。
        for (var pass = 0; pass < 4; pass++)
        {
            host.UpdateLayout();
            var viewportHeight = Math.Max(1, host.Viewport.Height);
            var point = _renderedBlocks[index].Control.TranslatePoint(
                new Point(0, _renderedBlocks[index].Control.Bounds.Height * value.BlockRatio), host);
            if (point is null) return false;
            var target = Math.Clamp(host.Offset.Y + point.Value.Y - viewportHeight / 2,
                0, Math.Max(0, host.Extent.Height - viewportHeight));
            if (Math.Abs(target - host.Offset.Y) < .5) return true;
            host.Offset = new Vector(host.Offset.X, target);
        }
        host.UpdateLayout();
        return true;
    }

    /// <summary>
    /// 失效远程图片缓存并重建受影响的图片块；本地文件与 data URI 保持不变。
    /// </summary>
    public void RefreshRemoteImages()
    {
        if (!TryRefreshRemoteImageBlocks())
        {
            QueueRenderDocument(MarkdownRenderMode.Full);
        }
    }

    /// <summary>
    /// 取用最近一次任务勾选回写对应的源码偏移（无待处理时返回 -1）。
    /// </summary>
    public int ConsumePendingTaskWriteBackOffset()
    {
        var offset = _taskMarkerOffset;
        _taskMarkerOffset = -1;
        return offset;
    }

    /// <summary>
    /// 当前文档大纲：标题层级、纯文本、1 起始源码行号与源码起始偏移。
    /// 宿主据此构建大纲侧栏，配合 <see cref="ScrollToSourceOffset"/> 跳转；
    /// 文档未渲染时为空。基于当前已渲染模型，流式过程中随快照更新。
    /// </summary>
    public IReadOnlyList<MarkdownOutlineItem> GetOutline() =>
        _documentHost is null ? [] : MarkdownOutlineExtractor.Extract(_renderedModel);

    /// <summary>
    /// 滚动到指定块的顶部（块序号 = <see cref="CurrentModel"/> 的块序）。
    /// 虚拟化宿主会先物化目标块；定位块尚未完成布局时返回 false，宿主可在
    /// 布局完成后重试（与 <see cref="RestoreReadingPosition"/> 相同的多轮收敛策略）。
    /// </summary>
    public bool ScrollToBlock(int blockIndex)
    {
        if (_documentHost is null || blockIndex < 0 || blockIndex >= _renderedBlocks.Count)
        {
            return false;
        }

        var host = ResolveScrollHost();
        if (host is null)
        {
            return false;
        }

        var index = Math.Clamp(blockIndex, 0, _renderedBlocks.Count - 1);
        _virtualizingHost?.PinItem(index);
        _virtualizingHost?.RealizeItem(index);
        for (var pass = 0; pass < 4; pass++)
        {
            host.UpdateLayout();
            if (_renderedBlocks[index].Control.TranslatePoint(new Point(0, 0), host) is not { } point)
            {
                return false;
            }

            var target = Math.Clamp(
                host.Offset.Y + point.Y,
                0, Math.Max(0, host.Extent.Height - host.Viewport.Height));
            if (Math.Abs(target - host.Offset.Y) < .5)
            {
                return true;
            }

            host.Offset = new Vector(host.Offset.X, target);
        }

        host.UpdateLayout();
        return true;
    }

    /// <summary>
    /// 滚动到源码偏移所在块的顶部（如 <see cref="GetOutline"/> 条目的
    /// <see cref="MarkdownOutlineItem.SourceOffset"/>）。偏移越界时钳制到最近块，
    /// 与阅读锚定的定位语义一致；文档未渲染时返回 false。
    /// </summary>
    public bool ScrollToSourceOffset(int sourceOffset)
    {
        var rendered = FindRenderedBlockBySourceOffset(sourceOffset);
        if (rendered is null)
        {
            return false;
        }

        return ScrollToBlock(_renderedBlocks.IndexOf(rendered));
    }

    private MarkdownDocumentModel ParseMarkdownWithTiming(string text)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var model = MarkdownParser.Parse(text, _documentPipeline);
        _lastParseDuration = System.Diagnostics.Stopwatch.GetElapsedTime(start);
        return model;
    }

    /// <summary>
    /// 属性变更后的渲染调度：全量模式立即重建；增量模式只登记快照，
    /// 由合并窗口与后台解析在结果到达时再做局部替换。
    /// </summary>
    private void ScheduleRender(MarkdownRenderMode mode)
    {
        if (_documentHost is null)
        {
            return;
        }

        var text = Markdown ?? string.Empty;
        if (mode == MarkdownRenderMode.Full)
        {
            RenderDocumentFull(text);
            return;
        }

        if (string.Equals(text, _renderedMarkdown, StringComparison.Ordinal))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(text) || _renderedBlocks.Count == 0)
        {
            RenderDocumentFull(text);
            return;
        }

        _scheduler.Queue(text);
    }

    private void OnSchedulerParseCompleted(object? sender, MarkdownParseResult result)
    {
        if (_documentHost is null)
        {
            return;
        }

        if (!string.Equals(result.Snapshot.Text, Markdown ?? string.Empty, StringComparison.Ordinal))
        {
            // 解析期间文本又变了：丢弃过期结果，等待下一次合并窗口。
            return;
        }

        _lastParseDuration = result.Duration;
        var dirtySpan = result.DirtySpanValid ? result.DirtySpan : (MarkdownTextSpan?)null;
        if (!result.IsFirstParse && TryRenderIncremental(result.Snapshot.Text, result.Model, dirtySpan))
        {
            return;
        }

        RenderDocumentFull(result.Snapshot.Text, result.Model);
    }

    private void OnSchedulerParseFailed(object? sender, Exception exception)
    {
        // 解析失败保持上一次渲染结果，不抛出到 UI 线程。
        System.Diagnostics.Debug.WriteLine($"Markdown parse failed: {exception}");
    }

    /// <summary>
    /// 流式源绑定切换：退订旧文档、订阅新文档并立即应用其当前快照。
    /// </summary>
    private void OnStreamingSourceChanged(Streaming.MarkdownStreamingDocument? oldValue, Streaming.MarkdownStreamingDocument? newValue)
    {
        if (ReferenceEquals(oldValue, newValue))
        {
            return;
        }

        if (oldValue is not null)
        {
            oldValue.SnapshotReady -= OnStreamingSnapshotReady;
        }

        if (newValue is null)
        {
            return;
        }

        newValue.SnapshotReady += OnStreamingSnapshotReady;
        if (newValue.Latest.Source.Length > 0)
        {
            OnStreamingSnapshotReady(newValue, newValue.Latest);
        }
        else
        {
            RenderDocumentFull(string.Empty);
        }
    }

    private void OnStreamingSnapshotReady(object? sender, Streaming.MarkdownStreamingSnapshot snapshot)
    {
        if (_documentHost is null)
        {
            return;
        }

        // 文档已被换绑的过期快照直接丢弃。
        if (!ReferenceEquals(sender, StreamingSource))
        {
            return;
        }

        _lastParseDuration = snapshot.ParseDuration;
        var text = snapshot.Model.Source;
        if (!TryRenderIncremental(text, snapshot.Model, null))
        {
            RenderDocumentFull(text, snapshot.Model);
        }
    }

    /// <summary>
    /// 命中结果：已渲染块序号、块类型、源码起始偏移与 1 起始的源码行号。
    /// </summary>
    public readonly record struct MarkdownBlockHit(int BlockIndex, MarkdownBlockKind Kind, int SourceStart, int SourceLine);

    /// <summary>
    /// 命中测试：<see cref="MarkdownViewer"/> 坐标系中的点落在哪个已渲染块内。
    /// 宿主据此实现「指针位置 → 源码行」类交互（如右键元素菜单），无需钻可视树；
    /// 未物化块（虚拟化离屏）与空白区域返回 false。
    /// </summary>
    public bool TryGetBlockAt(Point point, out MarkdownBlockHit hit)
    {
        hit = default;
        if (_documentHost is null)
        {
            return false;
        }

        for (var i = 0; i < _renderedBlocks.Count; i++)
        {
            var rendered = _renderedBlocks[i];
            if (rendered.Control.TranslatePoint(new Point(0, 0), this) is not { } origin)
            {
                continue;
            }

            if (!new Rect(origin, rendered.Control.Bounds.Size).Contains(point))
            {
                continue;
            }

            var modelBlock = i < _renderedModel.Blocks.Count ? _renderedModel.Blocks[i] : null;
            hit = new MarkdownBlockHit(
                i,
                modelBlock?.Kind ?? rendered.Kind,
                rendered.Start,
                GetSourceLineOfOffset(Markdown ?? string.Empty, rendered.Start));
            return true;
        }

        return false;
    }

    /// <summary>源码偏移 → 1 起始行号。</summary>
    private static int GetSourceLineOfOffset(string text, int offset)
    {
        var line = 1;
        var limit = Math.Clamp(offset, 0, text.Length);
        for (var i = 0; i < limit; i++)
        {
            if (text[i] == '\n')
            {
                line++;
            }
        }

        return line;
    }

    private ScrollViewer? ResolveScrollHost()
    {
        if (ScrollHost is not null)
        {
            return ScrollHost;
        }

        return this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
    }

    private bool TryGetSourceOffsetBoundsNow(int sourceOffset, out Rect bounds)
    {
        bounds = default;
        if (_documentHost is null || _renderedBlocks.Count == 0)
        {
            return false;
        }

        var renderedBlock = FindRenderedBlockBySourceOffset(sourceOffset);
        if (renderedBlock is null)
        {
            return false;
        }

        // 虚拟化宿主下目标块可能未物化：先物化，并把布局推进到最新，
        // 否则 TranslatePoint 拿到的是上一轮的位置（宿主滚动定位会出现「跳不准」）。
        var blockIndex = _renderedBlocks.IndexOf(renderedBlock);
        _virtualizingHost?.PinItem(blockIndex);
        _virtualizingHost?.RealizeItem(blockIndex);
        UpdateLayout();

        if (renderedBlock.Control.TranslatePoint(new Point(0, 0), this) is not { } topLeft)
        {
            return false;
        }

        bounds = new Rect(topLeft, renderedBlock.Control.Bounds.Size);
        return true;
    }

    private void OnTaskMarkerClick(CheckBox checkBox, bool isChecked)
    {
        if (!IsTaskEditingEnabled) return;
        if (checkBox.Tag is not int markerStart)
        {
            return;
        }

        var result = MarkdownTaskListWriter.SetTaskState(Markdown, markerStart, isChecked);
        if (result is null)
        {
            return;
        }

        _taskMarkerOffset = markerStart;
        TaskWriteBackRequested?.Invoke(this, result);
    }

    private void AttachTaskMarkerInteraction(CheckBox checkBox, int markerStart)
    {
        checkBox.Tag = markerStart;
        // 编译期可观察绑定等价替换字符串反射 Binding（AOT 门禁 IL2026/IL3050 欠账偿还）。
        checkBox.Bind(IsEnabledProperty, this.GetObservable(IsTaskEditingEnabledProperty));
        checkBox.IsHitTestVisible = true;
        checkBox.Click += OnTaskMarkerClickHandler;
        _currentBlockDisposables.Add(new TaskMarkerBinding(this, checkBox));
    }

    private void OnTaskMarkerClickHandler(object? sender, RoutedEventArgs e)
    {
        if (sender is CheckBox checkBox)
        {
            OnTaskMarkerClick(checkBox, checkBox.IsChecked == true);
        }
    }

    private static bool IsTaskMarkerInteractive(bool isTask, bool allowInteractiveTasks)
    {
        return isTask && allowInteractiveTasks;
    }

    bool Rendering.IMarkdownRenderContext.TaskMarkersInteractive => true;

    void Rendering.IMarkdownRenderContext.AttachTaskMarkerInteraction(CheckBox checkBox, int markerStart) =>
        AttachTaskMarkerInteraction(checkBox, markerStart);

    private bool TryRefreshRemoteImageBlocks()
    {
        if (_documentHost is null || _renderedBlocks.Count == 0)
        {
            return false;
        }

        for (var i = 0; i < _renderedBlocks.Count; i++)
        {
            var block = _renderedBlocks[i];
            if (!block.HasRemoteImage)
            {
                continue;
            }

            if (i >= _renderedModel.Blocks.Count)
            {
                return false;
            }

            var modelBlock = _renderedModel.Blocks[i];
            var syntaxStart = Math.Max(modelBlock.SyntaxBlock.Span.Start, 0);
            _currentBlockOrigin = Math.Max(modelBlock.SourceSpan.Start - syntaxStart, 0);
            var control = ConvertBlock(modelBlock.SyntaxBlock, _renderedMarkdown);
            if (control is null)
            {
                return false;
            }

            var blockDisposables = ExtractCurrentBlockDisposables(_currentBlockDisposables.Count);
            var replacement = RenderedBlock.FromModel(
                modelBlock,
                control,
                blockDisposables,
                ContainsRemoteImage(modelBlock),
                TryGetTaskMarkerOffset(_renderedMarkdown, modelBlock));
            block.Cleanup();
            ReplaceBlockControl(i, control);
            _renderedBlocks[i] = replacement;
        }

        ResetSelectionState();
        RefreshSelectionBlocks();
        InvalidateDocumentLayout();
        return true;
    }

    private static int TryGetTaskMarkerOffset(string? markdown, MarkdownDocumentBlock modelBlock)
    {
        return MarkdownTaskListWriter.TryLocateTaskMarker(
            markdown,
            modelBlock.SourceSpan.Start,
            modelBlock.SourceSpan.End,
            out var markerStart,
            out _)
            ? markerStart
            : -1;
    }

    /// <summary>
    /// 块内是否包含远程图片：命中时 <see cref="RefreshRemoteImages"/> 会重建该块。
    /// 近似判定（"![" 与 http(s) 同现）只会导致多做一次重建，不会漏刷新。
    /// </summary>
    internal static bool ContainsRemoteImage(MarkdownDocumentBlock block)
    {
        var source = block.SourceText;
        if (source.Length == 0 || !source.Contains("![", StringComparison.Ordinal))
        {
            return false;
        }

        return source.Contains("http://", StringComparison.OrdinalIgnoreCase)
               || source.Contains("https://", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TaskMarkerBinding : IDisposable
    {
        private readonly MarkdownViewer _viewer;
        private readonly CheckBox _checkBox;

        public TaskMarkerBinding(MarkdownViewer viewer, CheckBox checkBox)
        {
            _viewer = viewer;
            _checkBox = checkBox;
        }

        public void Dispose()
        {
            _checkBox.Click -= _viewer.OnTaskMarkerClickHandler;
            _checkBox.Tag = null;
        }
    }
}
