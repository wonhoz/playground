using System.Windows.Input;

namespace StarStrike.Engine;

/// <summary>
/// 키보드 입력 상태를 추적. KeyDown/KeyUp 이벤트를 바인딩하여 사용.
/// </summary>
public sealed class InputManager
{
    private readonly HashSet<Key> _pressed = [];

    public bool IsKeyDown(Key key) => _pressed.Contains(key);

    public void KeyDown(Key key) => _pressed.Add(key);
    public void KeyUp(Key key) => _pressed.Remove(key);
    public void Reset() => _pressed.Clear();

    // 편의 프로퍼티
    public bool Left => IsKeyDown(Key.Left) || IsKeyDown(Key.A);
    public bool Right => IsKeyDown(Key.Right) || IsKeyDown(Key.D);
    public bool Up => IsKeyDown(Key.Up) || IsKeyDown(Key.W);
    public bool Down => IsKeyDown(Key.Down) || IsKeyDown(Key.S);
    public bool Fire => IsKeyDown(Key.Space);
}
