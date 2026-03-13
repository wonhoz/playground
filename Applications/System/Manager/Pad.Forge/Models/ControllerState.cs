namespace PadForge.Models;

/// <summary>컨트롤러 실시간 상태 스냅샷</summary>
public class ControllerState
{
    public int Index { get; set; }
    public ControllerType Type { get; set; }
    public bool IsConnected { get; set; }

    // 버튼 비트 플래그
    public XButtons Buttons { get; set; }

    // 아날로그 (-1.0 ~ 1.0)
    public float LeftStickX { get; set; }
    public float LeftStickY { get; set; }
    public float RightStickX { get; set; }
    public float RightStickY { get; set; }

    // 트리거 (0.0 ~ 1.0)
    public float LeftTrigger { get; set; }
    public float RightTrigger { get; set; }

    // 배터리 (0~100, -1 = 유선/알 수 없음)
    public int BatteryLevel { get; set; } = -1;
    public bool IsWireless { get; set; }

    public bool IsButtonPressed(XButtons btn) => (Buttons & btn) != 0;

    /// <summary>현재 상태에서 활성화된 GamepadInput 목록 반환 (데드존 적용)</summary>
    public IEnumerable<GamepadInput> GetActiveInputs(float leftDz = 0.15f, float rightDz = 0.15f, float triggerDz = 0.05f)
    {
        if (IsButtonPressed(XButtons.DpadUp))        yield return GamepadInput.DpadUp;
        if (IsButtonPressed(XButtons.DpadDown))      yield return GamepadInput.DpadDown;
        if (IsButtonPressed(XButtons.DpadLeft))      yield return GamepadInput.DpadLeft;
        if (IsButtonPressed(XButtons.DpadRight))     yield return GamepadInput.DpadRight;
        if (IsButtonPressed(XButtons.Start))         yield return GamepadInput.Start;
        if (IsButtonPressed(XButtons.Back))          yield return GamepadInput.Back;
        if (IsButtonPressed(XButtons.LeftThumb))     yield return GamepadInput.LeftThumb;
        if (IsButtonPressed(XButtons.RightThumb))    yield return GamepadInput.RightThumb;
        if (IsButtonPressed(XButtons.LeftShoulder))  yield return GamepadInput.LeftShoulder;
        if (IsButtonPressed(XButtons.RightShoulder)) yield return GamepadInput.RightShoulder;
        if (IsButtonPressed(XButtons.A))             yield return GamepadInput.A;
        if (IsButtonPressed(XButtons.B))             yield return GamepadInput.B;
        if (IsButtonPressed(XButtons.X))             yield return GamepadInput.X;
        if (IsButtonPressed(XButtons.Y))             yield return GamepadInput.Y;

        if (LeftTrigger > triggerDz)                 yield return GamepadInput.LeftTrigger;
        if (RightTrigger > triggerDz)                yield return GamepadInput.RightTrigger;

        if (LeftStickY >  leftDz)                    yield return GamepadInput.LeftStickUp;
        if (LeftStickY < -leftDz)                    yield return GamepadInput.LeftStickDown;
        if (LeftStickX < -leftDz)                    yield return GamepadInput.LeftStickLeft;
        if (LeftStickX >  leftDz)                    yield return GamepadInput.LeftStickRight;

        if (RightStickY >  rightDz)                  yield return GamepadInput.RightStickUp;
        if (RightStickY < -rightDz)                  yield return GamepadInput.RightStickDown;
        if (RightStickX < -rightDz)                  yield return GamepadInput.RightStickLeft;
        if (RightStickX >  rightDz)                  yield return GamepadInput.RightStickRight;
    }
}
