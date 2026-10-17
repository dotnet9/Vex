namespace Vex.Modules.Workspace.Services;

/// <summary>源码、即时编辑和章节操作共用一条正文历史，视图重建不会丢失撤销。</summary>
public sealed class WorkspaceEditHistory
{
    public sealed record Snapshot(string Current, string[] Undo, string[] Redo);
    private readonly List<string> _undo = [];
    private readonly List<string> _redo = [];
    private string _current = "";
    private long _lastEdit;
    private bool _separate = true;
    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public void BreakGroup() => _separate = true;
    public void Reset(string text) { _current = text; _undo.Clear(); _redo.Clear(); BreakGroup(); }
    public void Observe(string text)
    {
        if (text == _current) return;
        var now = Environment.TickCount64;
        if (_separate || now - _lastEdit > 650 || Math.Abs(text.Length - _current.Length) > 32) _undo.Add(_current);
        while (_undo.Count > 200 || _undo.Count > 1 && _undo.Sum(item => (long)item.Length * 2) > 16_000_000) _undo.RemoveAt(0);
        _current = text; _redo.Clear(); _lastEdit = now; _separate = false;
    }
    public string? Undo() => Transfer(_undo, _redo);
    public string? Redo() => Transfer(_redo, _undo);
    private string? Transfer(List<string> from, List<string> to)
    {
        if (from.Count == 0) return null;
        to.Add(_current); _current = from[^1]; from.RemoveAt(from.Count - 1); BreakGroup(); return _current;
    }
    public Snapshot Capture() => new(_current, _undo.ToArray(), _redo.ToArray());
    public void Restore(Snapshot snapshot) { _current = snapshot.Current; _undo.Clear(); _undo.AddRange(snapshot.Undo); _redo.Clear(); _redo.AddRange(snapshot.Redo); BreakGroup(); }
}
