namespace StayAwake
{
    internal static class Program
    {
        /// <summary>
        /// 애플리케이션의 기본 진입점
        /// </summary>
        [STAThread]
        static void Main()
        {
            // 중복 실행 방지
            using var mutex = new Mutex(true, "StayAwake_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("StayAwake가 이미 실행 중입니다.", "StayAwake",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
