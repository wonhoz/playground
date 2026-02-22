namespace AiClip
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            using var mutex = new Mutex(true, "AiClip_SingleInstance", out bool isNew);
            if (!isNew)
            {
                MessageBox.Show("AI.Clip이 이미 실행 중입니다.", "AI.Clip",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayApplicationContext());
        }
    }
}
