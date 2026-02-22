using System.Windows.Input;

namespace DungeonDash.Engine;

public sealed class KeyInput
{
    private readonly HashSet<Key> _pressed = [];
    private readonly HashSet<Key> _justPressed = [];

    public bool IsDown(Key key) => _pressed.Contains(key);
    public bool JustPressed(Key key) => _justPressed.Remove(key);

    public void Press(Key key) { _pressed.Add(key); _justPressed.Add(key); }
    public void Release(Key key) => _pressed.Remove(key);
    public void Reset() { _pressed.Clear(); _justPressed.Clear(); }

    public bool Left => IsDown(Key.Left) || IsDown(Key.A);
    public bool Right => IsDown(Key.Right) || IsDown(Key.D);
    public bool Up => IsDown(Key.Up) || IsDown(Key.W);
    public bool Down => IsDown(Key.Down) || IsDown(Key.S);
    public bool Attack => JustPressed(Key.Z);
    public bool Dash => JustPressed(Key.X);
    public bool Skill => JustPressed(Key.C);
}
