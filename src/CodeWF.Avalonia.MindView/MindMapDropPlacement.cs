namespace CodeWF.Avalonia.MindView;

/// <summary>
/// 节点拖拽释放时相对目标节点的位置。
/// </summary>
public enum MindMapDropPlacement
{
    /// <summary>
    /// 插入到目标节点前面，成为同级节点。
    /// </summary>
    Before,

    /// <summary>
    /// 插入到目标节点后面，成为同级节点。
    /// </summary>
    After,

    /// <summary>
    /// 放到目标节点内部，成为子节点。
    /// </summary>
    Child
}
