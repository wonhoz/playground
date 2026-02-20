using System.Windows.Input;

namespace FistFury.Engine;

public sealed class KeyInput
{
    private readonly HashSet<Key> _pressed = [];

    public bool IsDown(Key key) => _pressed.Contains(key);
    public void Press(Key key) => _pressed.Add(key);
    public void Release(Key key) => _pressed.Remove(key);
    public void Reset() => _pressed.Clear();

    public bool Left => IsDown(Key.Left) || IsDown(Key.A);
    public bool Right => IsDown(Key.Right) || IsDown(Key.D);
    public bool Up => IsDown(Key.Up) || IsDown(Key.W);
    public bool Jump => IsDown(Key.Up) || IsDown(Key.W);
    public bool Punch => IsDown(Key.Z);
    public bool Kick => IsDown(Key.X);
    public bool Special => IsDown(Key.C);
}
