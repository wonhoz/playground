namespace StayAwake
{
    internal static class Program
    {
        // static 필드로 유지하여 GC에 의한 조기 해제 방지
        private static Mutex? _mutex;

        /// <summary>
        /// 애플리케이션의 기본 진입점
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 중복 실행 방지
            _mutex = new Mutex(true, "StayAwake_SingleInstance", out bool isNew);
            if (!isNew)
            {
                _mutex.Dispose();
                MessageBox.Show("StayAwake가 이미 실행 중입니다.", "StayAwake",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new TrayApplicationContext());
            }
            finally
            {
                _mutex.ReleaseMutex();
                _mutex.Dispose();
            }
        }
    }
}
