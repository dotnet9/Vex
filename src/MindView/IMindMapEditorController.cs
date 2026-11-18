namespace CodeWF.Avalonia.MindView;

/// <summary>
/// 脑图控件的宿主回调。普通场景可以只绑定 <c>MindMapEditor.Roots</c> 和
/// <c>SelectedNode</c> 使用控件内置操作；需要接入撤销、保存状态或业务规则时再实现本接口。
/// </summary>
public interface IMindMapEditorController
{
    /// <summary>
    /// 返回节点层级，根节点为 1。
    /// </summary>
    int GetLevel(MindMapNode node);

    /// <summary>
    /// 判断节点是否为中心主题。
    /// </summary>
    bool IsRoot(MindMapNode? node);

    /// <summary>
    /// 处理脑图标题输入框中的 Enter。默认根节点加子级，普通节点加同级。
    /// </summary>
    MindMapNode HandleMapEnter(MindMapNode node)
    {
        return IsRoot(node) ? AddChild(node, string.Empty) : AddSibling(node, string.Empty);
    }

    /// <summary>
    /// 处理脑图标题输入框中的 Tab。默认将节点降级为上一个同级节点的子节点。
    /// </summary>
    MindMapNode HandleMapTab(MindMapNode node)
    {
        DemoteNode(node);
        return node;
    }

    /// <summary>
    /// 给指定父节点添加子主题，并返回需要获得焦点的新节点。
    /// </summary>
    MindMapNode AddChild(MindMapNode? parent, string title = "");

    /// <summary>
    /// 在指定节点后添加同级主题，并返回需要获得焦点的新节点。
    /// </summary>
    MindMapNode AddSibling(MindMapNode? node, string title = "");

    /// <summary>
    /// 判断节点是否可提升为父节点的同级。
    /// </summary>
    bool CanPromoteNode(MindMapNode? node);

    /// <summary>
    /// 将节点提升一级，成功时返回 true。
    /// </summary>
    bool PromoteNode(MindMapNode? node);

    /// <summary>
    /// 判断节点是否可降级为上一个同级节点的子节点。
    /// </summary>
    bool CanDemoteNode(MindMapNode? node);

    /// <summary>
    /// 将节点降级一级，成功时返回 true。
    /// </summary>
    bool DemoteNode(MindMapNode? node);

    /// <summary>
    /// 判断节点是否可以在同级中上移。
    /// </summary>
    bool CanMoveNodeUp(MindMapNode? node) => false;

    /// <summary>
    /// 将节点在同级中上移。
    /// </summary>
    bool MoveNodeUp(MindMapNode? node) => false;

    /// <summary>
    /// 判断节点是否可以在同级中下移。
    /// </summary>
    bool CanMoveNodeDown(MindMapNode? node) => false;

    /// <summary>
    /// 将节点在同级中下移。
    /// </summary>
    bool MoveNodeDown(MindMapNode? node) => false;

    /// <summary>
    /// 删除节点，并返回删除后应该获得焦点的节点。
    /// </summary>
    MindMapNode DeleteNode(MindMapNode? node);

    /// <summary>
    /// 判断拖拽节点是否可以移动到目标节点附近。
    /// </summary>
    bool CanMoveNode(MindMapNode? node, MindMapNode? target);

    /// <summary>
    /// 按指定落点移动节点，成功时返回 true。
    /// </summary>
    bool MoveNode(MindMapNode? node, MindMapNode? target, MindMapDropPlacement placement);
}
