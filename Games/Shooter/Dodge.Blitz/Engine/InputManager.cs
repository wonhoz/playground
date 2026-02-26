using System.Windows.Input;

namespace DodgeBlitz.Engine;

/// <summary>
/// 키보드 입력 상태를 추적. KeyDown/KeyUp 이벤트를 바인딩하여 사용.
/// 조이스틱 방향은 JoystickManager가 매 프레임 JoyLeft/JoyRight/JoyUp/JoyDown에 기록.
/// </summary>
public sealed class GameInput
{
    private readonly HashSet<Key> _pressed = [];

    // 조이스틱 방향 상태 (JoystickManager가 OnUpdate마다 업데이트)
    internal bool JoyLeft, JoyRight, JoyUp, JoyDown;

    public bool IsKeyDown(Key key) => _pressed.Contains(key);

    public void KeyDown(Key key) => _pressed.Add(key);
    public void KeyUp(Key key) => _pressed.Remove(key);
    public void Reset()
    {
        _pressed.Clear();
        JoyLeft = JoyRight = JoyUp = JoyDown = false;
    }

    // 키보드 OR 조이스틱
    public bool Left  => IsKeyDown(Key.Left)  || IsKeyDown(Key.A) || JoyLeft;
    public bool Right => IsKeyDown(Key.Right) || IsKeyDown(Key.D) || JoyRight;
    public bool Up    => IsKeyDown(Key.Up)    || IsKeyDown(Key.W) || JoyUp;
    public bool Down  => IsKeyDown(Key.Down)  || IsKeyDown(Key.S) || JoyDown;
}
