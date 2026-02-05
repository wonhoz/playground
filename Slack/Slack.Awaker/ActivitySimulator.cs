using System.Runtime.InteropServices;

namespace StayAwake
{
    /// <summary>
    /// 활동 유형
    /// </summary>
    public enum ActivityType
    {
        /// <summary>마우스 이동만</summary>
        MouseMove,
        /// <summary>마우스 + 키보드</summary>
        MouseAndKeyboard
    }

    /// <summary>
    /// 마우스/키보드 활동 시뮬레이터
    /// Windows API를 사용하여 시스템 활동을 시뮬레이션
    /// </summary>
    public class ActivitySimulator
    {
        #region Windows API

        [DllImport("user32.dll")]
        private static extern bool SetCursorPos(int x, int y);

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);

        [DllImport("user32.dll")]
        private static extern void mouse_event(uint dwFlags, int dx, int dy, uint dwData, int dwExtraInfo);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        // 마우스 이벤트 플래그
        private const uint MOUSEEVENTF_MOVE = 0x0001;

        // 키보드 이벤트 플래그
        private const uint KEYEVENTF_KEYUP = 0x0002;

        // F15 키 (실제 키보드에 없어서 무해함)
        private const byte VK_F15 = 0x7E;

        // Scroll Lock (눈에 보이지만 대부분 사용 안 함)
        private const byte VK_SCROLL = 0x91;

        #endregion

        /// <summary>
        /// 활동 유형 설정
        /// </summary>
        public ActivityType ActivityType { get; set; } = ActivityType.MouseMove;

        /// <summary>
        /// 마우스 이동 거리 (픽셀)
        /// </summary>
        public int MoveDistance { get; set; } = 1;

        private bool _moveDirection = true;

        /// <summary>
        /// 활동 시뮬레이션 실행
        /// </summary>
        public void SimulateActivity()
        {
            try
            {
                // 마우스 이동
                MoveMouse();

                // 키보드 활동 (옵션)
                if (ActivityType == ActivityType.MouseAndKeyboard)
                {
                    PressF15Key();
                }
            }
            catch
            {
                // 실패해도 무시 (다음 타이머에서 재시도)
            }
        }

        /// <summary>
        /// 마우스를 1픽셀 이동 후 원위치
        /// </summary>
        private void MoveMouse()
        {
            if (GetCursorPos(out POINT currentPos))
            {
                // 방향 전환 (매번 반대 방향으로)
                int offset = _moveDirection ? MoveDistance : -MoveDistance;
                _moveDirection = !_moveDirection;

                // 이동
                SetCursorPos(currentPos.X + offset, currentPos.Y);

                // 아주 짧은 딜레이 후 원위치
                Thread.Sleep(50);
                SetCursorPos(currentPos.X, currentPos.Y);
            }
            else
            {
                // GetCursorPos 실패 시 mouse_event 사용
                mouse_event(MOUSEEVENTF_MOVE, 1, 0, 0, 0);
                Thread.Sleep(50);
                mouse_event(MOUSEEVENTF_MOVE, -1, 0, 0, 0);
            }
        }

        /// <summary>
        /// F15 키 누르기 (무해한 가상 키)
        /// F15는 실제 키보드에 없어서 다른 프로그램에 영향 없음
        /// </summary>
        private void PressF15Key()
        {
            keybd_event(VK_F15, 0, 0, 0);           // Key down
            Thread.Sleep(10);
            keybd_event(VK_F15, 0, KEYEVENTF_KEYUP, 0); // Key up
        }

        /// <summary>
        /// Scroll Lock 토글 (두 번 눌러서 원상복구)
        /// </summary>
        private void ToggleScrollLock()
        {
            // Scroll Lock 누르기
            keybd_event(VK_SCROLL, 0, 0, 0);
            keybd_event(VK_SCROLL, 0, KEYEVENTF_KEYUP, 0);
            Thread.Sleep(10);
            // 다시 눌러서 원상복구
            keybd_event(VK_SCROLL, 0, 0, 0);
            keybd_event(VK_SCROLL, 0, KEYEVENTF_KEYUP, 0);
        }
    }
}
