namespace StayAwake
{
    internal static class Program
    {
        /// <summary>
        /// 애플리케이션의 기본 진입점
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            // 아이콘 생성 모드
            if (args.Length > 0 && args[0] == "--generate-icons")
            {
                var resourcePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "Resources");
                IconGenerator.GenerateAllIcons(resourcePath);
                Console.WriteLine("아이콘 생성 완료!");
                return;
            }

            // 중복 실행 방지
            using var mutex = new Mutex(true, "StayAwake_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("StayAwake가 이미 실행 중입니다.", "StayAwake",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // 첫 실행 시 Resources 폴더가 없으면 생성
            EnsureResourcesExist();

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }

        /// <summary>
        /// Resources 폴더와 아이콘 파일이 없으면 생성
        /// </summary>
        private static void EnsureResourcesExist()
        {
            try
            {
                // 실행 파일 기준 Resources 폴더 경로
                var exePath = AppContext.BaseDirectory;
                var resourcePath = Path.Combine(exePath, "Resources");

                // Resources 폴더가 없거나 app.ico가 없으면 생성
                var appIcoPath = Path.Combine(resourcePath, "app.ico");
                if (!File.Exists(appIcoPath))
                {
                    IconGenerator.GenerateAllIcons(resourcePath);
                }
            }
            catch
            {
                // 아이콘 생성 실패해도 앱은 계속 실행
            }
        }
    }
}
