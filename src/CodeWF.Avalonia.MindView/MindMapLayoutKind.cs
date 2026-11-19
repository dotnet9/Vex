namespace CodeWF.Avalonia.MindView;

/// <summary>导图布局：几何只改变节点排布，不改变文档结构顺序。</summary>
public enum MindMapLayoutKind
{
    /// <summary>单侧树形（默认）。</summary>
    Right,

    /// <summary>双侧平衡：一级分支按子树负载分到左右两侧。</summary>
    Balanced,

    /// <summary>中心放射：一级分支按叶子数分配角度。</summary>
    Radial,

    /// <summary>向下树形：层级自上而下展开。</summary>
    Down,

    /// <summary>目录树：按深度缩进的纵向列表。</summary>
    Outline,

    /// <summary>鱼骨图：一级分支沿主脊上下交替。</summary>
    Fishbone,
}

/// <summary>导图样式：只调整节点视觉（底色/描边/圆角/字重），不改变布局与结构。</summary>
public enum MindMapStyleKind
{
    /// <summary>清雅（默认）。</summary>
    Quiet,

    /// <summary>缤纷：分支底色用主题强调色的高饱和变体。</summary>
    Colorful,

    /// <summary>线框：透明底 + 描边卡片。</summary>
    Outline,

    /// <summary>胶囊：全圆角卡片。</summary>
    Capsule,

    /// <summary>脉络：无底卡片 + 标题下划线装饰。</summary>
    Underline,

    /// <summary>黑白：去彩度的灰阶卡片。</summary>
    Mono,
}
