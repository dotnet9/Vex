using System;
using System.Collections.Generic;
using System.Linq;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.VisualTree;

// 与 Controls/CodeWF.Avalonia.Controls/Controls/WindowedStackPanel.cs 互为副本：
// Markdown 包不依赖 Controls 主工程（斩断 Semi 传递依赖），修改任一侧须评估同步另一侧。
namespace CodeWF.Avalonia.Markdown.Controls;

/// <summary>
/// 窗口化堆叠面板：子项数量超过阈值后，只把「视口 ± overscan」范围内的子项控件挂到可视树，
/// 其余子项保留占位尺寸，滚动离屏即释放控件，避免长列表一次性挂载全部控件。
/// <para>
/// 与 Avalonia 自带 <c>VirtualizingStackPanel</c> 的差别：本面板的子项索引由调用方维护，
/// 即使某项当前未物化，索引也不会错位（<see cref="InsertItem"/> / <see cref="RemoveAt"/> 按索引操作），
/// 适合「文档块」这类由业务侧按语义增删、又需要按视口裁剪的场景。
/// </para>
/// <para>
/// 变高子项（文档块）的解体策略：<see cref="ResolveInPlace"/> 只在「原本物化的子项彻底离开
/// 视口窗口」时才把它列入解体候选，overscan 边缘进出不会反复物化/解体；再用
/// <see cref="KeepAliveBudget"/> 的尺寸预算优先保留大块（图片、表格、图表），
/// 避免滚动时把已解码/已光栅化的大块丢掉、回来时重建。
/// </para>
/// <para>
/// 关闭 <see cref="EnableVirtualization"/> 或子项数低于 <see cref="VirtualizationThreshold"/> 时，
/// 行为与普通垂直面板一致，可在异常场景一开关退回非虚拟化宿主。
/// </para>
/// </summary>
public class WindowedStackPanel : Panel
{
    private sealed class ItemEntry
    {
        public ItemEntry(Control control, bool pinned, bool pinnedByKind)
        {
            Control = control;
            Pinned = pinned;
            PinnedByKind = pinnedByKind;
        }

        public Control Control { get; }

        /// <summary>调用方指定的固定物化（如当前滚动锚点所在项）。</summary>
        public bool Pinned { get; set; }

        /// <summary>按子项类型固定物化（如代码块、表格、图片等大块），避免测量与滚动抖动。</summary>
        public bool PinnedByKind { get; }

        public bool ShouldAlwaysRealize => Pinned || PinnedByKind;

        public bool Realized { get; set; } = true;

        public bool Measured { get; set; }

        /// <summary>上一帧布局时是否落在视口窗口内（判定「彻底离开」用）。</summary>
        public bool InWindow { get; set; } = true;

        public double Size { get; set; }
    }

    private readonly List<ItemEntry> _entries = [];

    /// <summary>
    /// 宿主直接挂进 <see cref="Panel.Children"/>、没有登记为条目的装饰子项（表格工具条这类）。
    /// 它不属于任何索引序列，但必须占位：否则排位游标与 <c>Children</c> 下标一错位，
    /// 后面的块会拿到邻居的高度，正文尾部被下一块盖掉。
    /// </summary>
    private sealed class DecoratorEntry
    {
        public DecoratorEntry(int band, Control control, bool above)
        {
            Band = band;
            Control = control;
            Above = above;
        }

        /// <summary>挂靠的条目索引（装饰排在它之上或之下）。</summary>
        public int Band { get; }

        public Control Control { get; }

        /// <summary>排在所属条目之上（工具条在表格上方）；false 表示排在末条之下。</summary>
        public bool Above { get; }

        /// <summary>本轮量测出的高度（含自身外边距），排位与条带总高都用它。</summary>
        public double Height { get; set; }
    }

    private readonly List<DecoratorEntry> _decorators = [];
    private double[] _decoratorHeights = [];
    private ScrollViewer? _subscribedScrollHost;
    private double _lastWidth = double.NaN;
    private double[] _sizeBuffer = [];
    private bool[] _pinnedBuffer = [];
    private bool[] _realizedBuffer = [];
    private bool[] _inWindowBuffer = [];
    private bool[] _candidateBuffer = [];
    private double _anchorOffsetY = double.NaN;
    private double _anchorTop;
    private int _anchorIndex;
    private double _lastOffsetY = double.NaN;
    private bool _scrollDown = true;

    public static readonly StyledProperty<bool> EnableVirtualizationProperty =
        AvaloniaProperty.Register<WindowedStackPanel, bool>(nameof(EnableVirtualization), true);

    /// <summary>启用虚拟化的最小子项数量；低于该值按普通面板处理，避免小列表付出额外开销。</summary>
    public static readonly StyledProperty<int> VirtualizationThresholdProperty =
        AvaloniaProperty.Register<WindowedStackPanel, int>(nameof(VirtualizationThreshold), 40);

    /// <summary>视口上下额外物化的屏数。</summary>
    public static readonly StyledProperty<double> OverscanViewportsProperty =
        AvaloniaProperty.Register<WindowedStackPanel, double>(nameof(OverscanViewports), 2d);

    /// <summary>滚动宿主；未显式指定时自动向上查找。</summary>
    public static readonly StyledProperty<ScrollViewer?> ScrollHostProperty =
        AvaloniaProperty.Register<WindowedStackPanel, ScrollViewer?>(nameof(ScrollHost));

    /// <summary>子项间距。</summary>
    public static readonly StyledProperty<double> SpacingProperty =
        AvaloniaProperty.Register<WindowedStackPanel, double>(nameof(Spacing));

    /// <summary>尚未测量过的子项使用的估算尺寸（经验值，量测后回填）。</summary>
    public static readonly StyledProperty<double> EstimatedItemHeightProperty =
        AvaloniaProperty.Register<WindowedStackPanel, double>(nameof(EstimatedItemHeight), 24d);

    /// <summary>
    /// 离屏大块的像素高度预算：已测量的子项彻底离开视口窗口后，
    /// 尺寸不小于 <see cref="KeepAliveMinItemHeight"/> 者按「从大到小」保留到本预算用尽，
    /// 其余立即解体。0 表示不保留（全部立即解体）。
    /// </summary>
    public static readonly StyledProperty<double> KeepAliveBudgetProperty =
        AvaloniaProperty.Register<WindowedStackPanel, double>(nameof(KeepAliveBudget), 0d);

    /// <summary>计入保留预算的最小高度；小于该值的子项离开窗口即解体。</summary>
    public static readonly StyledProperty<double> KeepAliveMinItemHeightProperty =
        AvaloniaProperty.Register<WindowedStackPanel, double>(nameof(KeepAliveMinItemHeight), 0d);

    /// <summary>
    /// 尺寸未确定（尚未测量）离屏子项的最大保留数量：优先保留滚动方向上「接下来要滚回来」
    /// 的那一侧。这类子项按估算高度占位，保留它们不改变总高度。
    /// </summary>
    public static readonly StyledProperty<int> KeepAliveUnknownCountProperty =
        AvaloniaProperty.Register<WindowedStackPanel, int>(nameof(KeepAliveUnknownCount), 0);

    /// <summary>
    /// 保留候选判定：返回 true 的子项才参与 <see cref="KeepAliveBudget"/> 与
    /// <see cref="KeepAliveUnknownCount"/> 的保留。未设置时全部子项都是候选
    /// （适合「离屏即销毁」代价都差不多的长列表）；文档块这类场景由宿主只把重块标为候选，
    /// 让轻量段落照常解体。
    /// </summary>
    public Func<int, bool>? KeepAliveCandidateResolver { get; set; }

    public bool EnableVirtualization
    {
        get => GetValue(EnableVirtualizationProperty);
        set => SetValue(EnableVirtualizationProperty, value);
    }

    public int VirtualizationThreshold
    {
        get => GetValue(VirtualizationThresholdProperty);
        set => SetValue(VirtualizationThresholdProperty, value);
    }

    public double OverscanViewports
    {
        get => GetValue(OverscanViewportsProperty);
        set => SetValue(OverscanViewportsProperty, value);
    }

    public ScrollViewer? ScrollHost
    {
        get => GetValue(ScrollHostProperty);
        set => SetValue(ScrollHostProperty, value);
    }

    public double Spacing
    {
        get => GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    public double EstimatedItemHeight
    {
        get => GetValue(EstimatedItemHeightProperty);
        set => SetValue(EstimatedItemHeightProperty, value);
    }

    public double KeepAliveBudget
    {
        get => GetValue(KeepAliveBudgetProperty);
        set => SetValue(KeepAliveBudgetProperty, value);
    }

    public double KeepAliveMinItemHeight
    {
        get => GetValue(KeepAliveMinItemHeightProperty);
        set => SetValue(KeepAliveMinItemHeightProperty, value);
    }

    public int KeepAliveUnknownCount
    {
        get => GetValue(KeepAliveUnknownCountProperty);
        set => SetValue(KeepAliveUnknownCountProperty, value);
    }

    /// <summary>当前子项数量（含未物化项）。</summary>
    public int ItemCount => _entries.Count;

    /// <summary>当前是否实际处于虚拟化状态（开关打开且子项数达到阈值）。</summary>
    public bool IsVirtualizing => EnableVirtualization && _entries.Count >= Math.Max(1, VirtualizationThreshold);

    /// <summary>已物化（挂在可视树上）的子项数量，供诊断与测试使用。</summary>
    public int RealizedItemCount => Children.Count;

    /// <summary>滚动偏移被回补（布局锚点补偿）的次数，供诊断与测试使用。</summary>
    public int AnchorCompensationCount { get; private set; }

    /// <summary>当前被调用方固定物化的子项数量，供诊断与测试使用。</summary>
    public int PinnedItemCount => _entries.Count(entry => entry.Pinned);

    static WindowedStackPanel()
    {
        AffectsMeasure<WindowedStackPanel>(
            EnableVirtualizationProperty,
            VirtualizationThresholdProperty,
            OverscanViewportsProperty,
            EstimatedItemHeightProperty,
            KeepAliveBudgetProperty,
            KeepAliveMinItemHeightProperty,
            KeepAliveUnknownCountProperty,
            SpacingProperty);
        AffectsArrange<WindowedStackPanel>(SpacingProperty);
    }

    public void AddItem(Control control, bool pinned = false, bool pinnedByKind = false) =>
        InsertItem(_entries.Count, control, pinned, pinnedByKind);

    public void InsertItem(int index, Control control, bool pinned = false, bool pinnedByKind = false)
    {
        var entry = new ItemEntry(control, pinned, pinnedByKind);
        index = Math.Clamp(index, 0, _entries.Count);
        _entries.Insert(index, entry);
        Children.Insert(RealizedIndexBefore(index), control);
        entry.Realized = true;
        InvalidateMeasure();
    }

    /// <summary>
    /// 在锚点子项之前插入门兄弟项。返回 false 表示锚点不是本面板子项。
    /// 注意：插入会把锚点及其后所有条目整体后移一位。宿主若另有一份与条目下标对齐的
    /// 数据序列（如按块号增删的渲染表），插入必须同步那一侧，否则按号替换会落错块。
    /// 只想在某个块旁边挂一块装饰（工具条、角标）又不想动条目下标时，直接把它加进
    /// <see cref="Panel.Children"/> 即可：未登记的子项会被当作所属条目的附加高度排布
    /// （见 <see cref="CollectDecorators"/>），不再造成排位串位。
    /// </summary>
    public bool InsertItemBefore(Control anchor, Control control, bool pinned = false, bool pinnedByKind = false)
    {
        var index = _entries.FindIndex(entry => ReferenceEquals(entry.Control, anchor));
        if (index < 0)
        {
            return false;
        }

        InsertItem(index, control, pinned, pinnedByKind);
        return true;
    }

    public void RemoveAt(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return;
        }

        var entry = _entries[index];
        if (entry.Realized)
        {
            Children.Remove(entry.Control);
            entry.Realized = false;
        }

        _entries.RemoveAt(index);
        InvalidateMeasure();
    }

    public void ClearItems()
    {
        _anchorOffsetY = double.NaN;
        _anchorIndex = 0;
        _entries.Clear();
        Children.Clear();
        _decorators.Clear();
        InvalidateMeasure();
    }

    /// <summary>取子项控件；未物化时返回 null。</summary>
    public Control? GetRealizedItem(int index) =>
        index >= 0 && index < _entries.Count && _entries[index].Realized ? _entries[index].Control : null;

    /// <summary>
    /// 请求物化指定子项（偏移映射等需要精确 Bounds 的场景），返回该子项是否存在。
    /// </summary>
    public bool RealizeItem(int index)
    {
        if (index < 0 || index >= _entries.Count)
        {
            return false;
        }

        var entry = _entries[index];
        if (entry.Realized)
        {
            return true;
        }

        entry.Realized = true;
        Children.Insert(RealizedIndexBefore(index), entry.Control);
        InvalidateMeasure();
        return true;
    }

    /// <summary>
    /// 固定物化指定子项（如当前滚动锚点，离开窗口也保留控件与解码结果），
    /// 取消固定用 <see cref="ReleaseItem"/>。固定项不参与解体与保留预算。
    /// </summary>
    public void PinItem(int index, bool pinned = true)
    {
        if (index < 0 || index >= _entries.Count || _entries[index].Pinned == pinned)
        {
            return;
        }

        _entries[index].Pinned = pinned;
        InvalidateMeasure();
    }

    /// <summary>取消 <see cref="PinItem"/> 设置的固定物化。</summary>
    public void ReleaseItem(int index) => PinItem(index, false);

    /// <summary>
    /// 取消全部固定物化；调用方在整篇内容重建（全文重渲、切换文档）前调用，
    /// 避免上一次的滚动锚点按索引残留在新内容上。
    /// </summary>
    public void DetachAllPinned()
    {
        var changed = false;
        foreach (var entry in _entries)
        {
            if (entry.Pinned)
            {
                entry.Pinned = false;
                changed = true;
            }
        }

        if (changed)
        {
            InvalidateMeasure();
        }
    }

    /// <summary>取子项在滚动方向上的顶边（未物化项用已测量/估算尺寸推算）。</summary>
    public bool TryGetItemTop(int index, out double top)
    {
        top = 0;
        if (index < 0 || index >= _entries.Count)
        {
            return false;
        }

        for (var i = 0; i < index; i++)
        {
            top += BandSize(i) + Spacing;
        }

        return true;
    }

    /// <summary>子项尺寸：已量测优先，其次估算。</summary>
    public double GetItemHeight(int index) =>
        index >= 0 && index < _entries.Count ? BandSize(index) : 0d;

    /// <summary>该子项是否已经量测出真实尺寸（否则按 <see cref="EstimatedItemHeight"/> 占位）。</summary>
    public bool IsItemMeasured(int index) =>
        index >= 0 && index < _entries.Count && _entries[index].Measured;

    protected override Size MeasureOverride(Size availableSize)
    {
        PrepareAnchor();
        CollectDecorators();
        var virtualizing = IsVirtualizing;
        // Auto 列先按无限宽查询自然尺寸；短文档须把该约束传给图片和文本。
        var width = double.IsFinite(availableSize.Width) || !virtualizing ? availableSize.Width : _lastWidth;
        if (virtualizing && !double.IsFinite(width))
        {
            width = 0;
        }

        var (windowTop, windowBottom) = ResolveVisibleWindow(virtualizing, availableSize);
        var isVisibleWindow = !double.IsNegativeInfinity(windowTop) && !double.IsPositiveInfinity(windowBottom);

        // 解体候选基于「上一帧是否已物化」+「本帧视口窗口判定」：
        // 本帧刚离开窗口的块才是候选，必须在同一帧决定保留还是解体，
        // 否则它已被解体，下一帧 realized 为 false，就再也回不到保留逻辑里。
        var sizes = FillSizeBuffer();
        var pinned = FillPinnedBuffer();
        var realized = FillRealizedBuffer();
        var inWindow = FillInWindowBuffer();
        var windowRealization = WindowedRealization.Resolve(sizes, pinned, Spacing, windowTop, windowBottom);
        var keepAlive = WindowedRealization.ResolveInPlace(
            sizes,
            pinned,
            realized,
            windowRealization,
            isVisibleWindow,
            KeepAliveBudget,
            KeepAliveMinItemHeight,
            KeepAliveUnknownCount,
            FillCandidateBuffer(),
            _scrollDown);

        double y = 0;
        var maxWidth = 0d;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            var realize = windowRealization[i] || keepAlive[i];
            entry.InWindow = windowRealization[i];
            SetRealized(entry, realize);

            if (entry.Realized)
            {
                entry.Control.Measure(new Size(width, double.PositiveInfinity));
                var desired = entry.Control.DesiredSize;
                entry.Size = desired.Height;
                entry.Measured = true;
                maxWidth = Math.Max(maxWidth, desired.Width);
            }

            maxWidth = Math.Max(maxWidth, MeasureDecorators(i, width));
            y += ResolveSize(entry) + _decoratorHeights[i] + Spacing;
        }

        if (_entries.Count > 0)
        {
            y -= Spacing;
        }

        if (double.IsFinite(width)) _lastWidth = width;
        var size = new Size(maxWidth, Math.Max(0, y));
        CompensateAnchor(size.Height);
        return size;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double y = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            var entry = _entries[i];
            // 条带 = 装饰总高 + 条目槽位。排位按引用取 entry.Control，绝不再按 Children 下标取：
            // 下标一旦和条目错位，邻居的高度就排到了错的块上（表格被裁、末段被下一块盖住）。
            var cursor = y;
            cursor = ArrangeDecorators(i, cursor, finalSize.Width, true);
            if (entry.Realized)
            {
                var height = entry.Measured ? entry.Size : entry.Control.DesiredSize.Height;
                entry.Control.Arrange(new Rect(0, cursor, finalSize.Width, Math.Max(0, height)));
                cursor += height;
            }

            ArrangeDecorators(i, cursor, finalSize.Width, false);
            y += ResolveSize(entry) + _decoratorHeights[i] + Spacing;
        }

        return finalSize;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SubscribeScrollHost();
        _anchorOffsetY = double.NaN;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        UnsubscribeScrollHost();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ScrollHostProperty)
        {
            SubscribeScrollHost();
        }
    }

    private void SubscribeScrollHost()
    {
        var host = ScrollHost ?? this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (ReferenceEquals(host, _subscribedScrollHost))
        {
            return;
        }

        UnsubscribeScrollHost();
        _subscribedScrollHost = host;
        if (host is not null)
        {
            host.ScrollChanged += OnScrollHostChanged;
        }
    }

    private void UnsubscribeScrollHost()
    {
        if (_subscribedScrollHost is not null)
        {
            _subscribedScrollHost.ScrollChanged -= OnScrollHostChanged;
            _subscribedScrollHost = null;
        }
    }

    /// <summary>
    /// 滚动只标记「下一帧需要重新量测」（由 Measure 决定窗口内外的物化变化），
    /// 不做任何同步布局；偏移抖动交给 <see cref="CompensateAnchor"/> 按锚点处理。
    /// </summary>
    private void OnScrollHostChanged(object? sender, ScrollChangedEventArgs e)
    {
        if (!IsVirtualizing)
        {
            return;
        }

        // 记住滚动方向：保留未知尺寸子项时优先「将要滚回来」的那一侧。
        if (sender is ScrollViewer host)
        {
            var offsetY = host.Offset.Y;
            if (double.IsFinite(_lastOffsetY) && Math.Abs(offsetY - _lastOffsetY) > 0.5)
            {
                _scrollDown = offsetY > _lastOffsetY;
            }

            _lastOffsetY = offsetY;
        }

        InvalidateMeasure();
    }

    private double[] FillSizeBuffer()
    {
        if (_sizeBuffer.Length != _entries.Count)
        {
            _sizeBuffer = new double[_entries.Count];
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            _sizeBuffer[i] = BandSize(i);
        }

        return _sizeBuffer;
    }

    private bool[] FillPinnedBuffer()
    {
        if (_pinnedBuffer.Length != _entries.Count)
        {
            _pinnedBuffer = new bool[_entries.Count];
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            _pinnedBuffer[i] = _entries[i].ShouldAlwaysRealize;
        }

        return _pinnedBuffer;
    }

    private bool[] FillRealizedBuffer()
    {
        if (_realizedBuffer.Length != _entries.Count)
        {
            _realizedBuffer = new bool[_entries.Count];
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            _realizedBuffer[i] = _entries[i].Realized;
        }

        return _realizedBuffer;
    }

    private bool[] FillInWindowBuffer()
    {
        if (_inWindowBuffer.Length != _entries.Count)
        {
            _inWindowBuffer = new bool[_entries.Count];
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            _inWindowBuffer[i] = _entries[i].InWindow;
        }

        return _inWindowBuffer;
    }

    /// <summary>保留候选掩码；未设置判定委托时返回 null（全部子项都是候选）。</summary>
    private bool[]? FillCandidateBuffer()
    {
        if (KeepAliveCandidateResolver is not { } resolver)
        {
            return null;
        }

        if (_candidateBuffer.Length != _entries.Count)
        {
            _candidateBuffer = new bool[_entries.Count];
        }

        for (var i = 0; i < _entries.Count; i++)
        {
            _candidateBuffer[i] = resolver(i);
        }

        return _candidateBuffer;
    }

    private (double Top, double Bottom) ResolveVisibleWindow(bool virtualizing, Size availableSize)
    {
        if (!virtualizing)
        {
            return (double.NegativeInfinity, double.PositiveInfinity);
        }

        var host = _subscribedScrollHost ?? ScrollHost;
        if (host is null)
        {
            return (double.NegativeInfinity, double.PositiveInfinity);
        }

        var viewportHeight = host.Viewport.Height;
        if (!double.IsFinite(viewportHeight) || viewportHeight <= 0)
        {
            viewportHeight = double.IsFinite(availableSize.Height) && availableSize.Height > 0
                ? availableSize.Height
                : 720;
        }

        return WindowedRealization.ResolveWindow(host.Offset.Y, viewportHeight, OverscanViewports);
    }

    /// <summary>
    /// 布局锚点补偿：变高子项（图片、公式、图表）量测后总高度会变化，
    /// 若锚点（所有尺寸变化项之上最近的已量测子项）顶边发生位移，
    /// 则把滚动偏移回补同样的差值，使视口内容不因量测而抖动。
    /// </summary>
    private void CompensateAnchor(double extentHeight)
    {
        var host = _subscribedScrollHost ?? ScrollHost;
        if (host is null)
        {
            _anchorOffsetY = double.NaN;
            return;
        }

        var offsetY = host.Offset.Y;
        if (!double.IsFinite(_anchorOffsetY))
        {
            _anchorTop = FindAnchorTop();
            _anchorOffsetY = offsetY;
            return;
        }

        if (Math.Abs(_anchorOffsetY - offsetY) >= 0.5)
        {
            // 用户滚动或宿主主动定位：锚点跟随新偏移，不做补偿。
            _anchorOffsetY = offsetY;
            _anchorTop = FindAnchorTop();
            return;
        }

        var shift = FindAnchorTop() - _anchorTop;
        if (Math.Abs(shift) < 0.5)
        {
            return;
        }

        var maxOffset = Math.Max(0, extentHeight - host.Viewport.Height);
        var target = Math.Clamp(offsetY + shift, 0, maxOffset);
        _anchorTop += target - offsetY;
        _anchorOffsetY = target;
        if (Math.Abs(target - offsetY) < 0.5)
        {
            return;
        }

        host.Offset = new Vector(host.Offset.X, target);
        AnchorCompensationCount++;
    }

    /// <summary>
    /// 在量测前记录视口顶端所在项。不能锚定文档末尾，否则后续块变高会把首屏推向末尾。
    /// </summary>
    private void PrepareAnchor()
    {
        var host = _subscribedScrollHost ?? ScrollHost;
        if (host is null) return;
        if (double.IsFinite(_anchorOffsetY) && Math.Abs(_anchorOffsetY - host.Offset.Y) < .5) return;
        _anchorOffsetY = host.Offset.Y;
        var offset = Math.Max(0, host.Offset.Y - Margin.Top);
        var top = 0d; _anchorIndex = 0;
        for (var i = 0; i < _entries.Count; i++)
        {
            if (top > offset) break;
            _anchorIndex = i;
            top += BandSize(i) + Spacing;
        }
        _anchorTop = FindAnchorTop();
    }

    private double FindAnchorTop()
    {
        var top = 0d;
        for (var i = 0; i < Math.Min(_anchorIndex, _entries.Count); i++)
            top += BandSize(i) + Spacing;
        return top;
    }

    private void SetRealized(ItemEntry entry, bool realized)
    {
        if (entry.Realized == realized)
        {
            return;
        }

        if (realized)
        {
            entry.Realized = true;
            Children.Insert(RealizedIndexBefore(_entries.IndexOf(entry)), entry.Control);
        }
        else
        {
            // Size 缓存 Measure 得到的 DesiredSize（包含 Margin）。Bounds 不含 Margin，
            // 用它覆盖会在回收/物化之间反复缩小/增大 extent，触发锚点补偿和无限布局循环。
            Children.Remove(entry.Control);
            entry.Realized = false;
        }
    }

    /// <summary>
    /// 新物化/插入的子项应落在 <c>Children</c> 的哪个下标：取其后第一个已登记子项的实际位置。
    /// 不能数「之前有多少个已登记条目」——宿主直接挂进来的装饰也占 Children 下标，
    /// 数条目会把工具条挤到它所属表格的后面去。
    /// </summary>
    private int RealizedIndexBefore(int entryIndex)
    {
        for (var i = entryIndex; i < _entries.Count; i++)
        {
            if (!_entries[i].Realized)
            {
                continue;
            }

            var at = Children.IndexOf(_entries[i].Control);
            if (at >= 0)
            {
                return at;
            }
        }

        return Children.Count;
    }

    private double ResolveSize(ItemEntry entry) =>
        entry.Measured ? entry.Size : Math.Max(0, EstimatedItemHeight);

    /// <summary>条目在文档空间里实际占的高：自身槽位加上挂在它身上的装饰。</summary>
    private double BandSize(int index) =>
        ResolveSize(_entries[index]) + DecoratorHeight(index);

    private double DecoratorHeight(int band) =>
        band >= 0 && band < _decoratorHeights.Length ? _decoratorHeights[band] : 0d;

    /// <summary>
    /// 找出宿主直接挂进 <see cref="Panel.Children"/> 的未登记子项，按文档顺序归到紧随其后的条目名下。
    /// 单遍扫描：已登记子项在 Children 里的相对顺序与条目顺序一致（物化时按
    /// <see cref="RealizedIndexBefore"/> 插入），所以条目游标只需前进，先跳过不在树上的未物化条目。
    /// </summary>
    private void CollectDecorators()
    {
        _decorators.Clear();
        if (_decoratorHeights.Length != _entries.Count)
        {
            _decoratorHeights = new double[_entries.Count];
        }
        else
        {
            Array.Clear(_decoratorHeights);
        }

        if (_entries.Count == 0)
        {
            return;
        }

        var band = 0;
        foreach (var child in Children)
        {
            while (band < _entries.Count && !_entries[band].Realized)
            {
                band++;
            }

            if (band < _entries.Count && ReferenceEquals(_entries[band].Control, child))
            {
                band++;
                continue;
            }

            _decorators.Add(new DecoratorEntry(
                band < _entries.Count ? band : _entries.Count - 1,
                child,
                band < _entries.Count));
        }
    }

    /// <summary>量测挂在指定条目上的装饰，回填 <see cref="_decoratorHeights"/>，返回它们的最大宽度。</summary>
    private double MeasureDecorators(int band, double width)
    {
        if (_decorators.Count == 0)
        {
            return 0d;
        }

        var height = 0d;
        var maxWidth = 0d;
        foreach (var decorator in _decorators)
        {
            if (decorator.Band != band)
            {
                continue;
            }

            decorator.Control.Measure(new Size(width, double.PositiveInfinity));
            decorator.Height = decorator.Control.DesiredSize.Height;
            height += decorator.Height;
            maxWidth = Math.Max(maxWidth, decorator.Control.DesiredSize.Width);
        }

        _decoratorHeights[band] = height;
        return maxWidth;
    }

    /// <summary>把指定条带上的一侧装饰依次排下去，返回新的游标。</summary>
    private double ArrangeDecorators(int band, double cursor, double width, bool above)
    {
        foreach (var decorator in _decorators)
        {
            if (decorator.Band != band || decorator.Above != above)
            {
                continue;
            }

            decorator.Control.Arrange(new Rect(0, cursor, width, Math.Max(0, decorator.Height)));
            cursor += decorator.Height;
        }

        return cursor;
    }
}
