using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace Music.Player
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\MusicPlayer_SingleInstance_7F3A2B1C";
        private const string PipeName = "MusicPlayer_Pipe_7F3A2B1C";
        private static Mutex? _mutex;

        [STAThread]
        public static void Main(string[] args)
        {
            // 앱 시작 전에 Mutex 체크 (가장 빠른 시점)
            _mutex = new Mutex(true, MutexName, out bool isFirstInstance);

            if (!isFirstInstance)
            {
                // 이미 실행 중 - 파일 경로를 기존 인스턴스에 전달하고 종료
                SendArgsToRunningInstance(args);
                return;
            }

            // 첫 번째 인스턴스 - 앱 시작
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 파이프 서버 시작 (다른 인스턴스로부터 파일 경로 수신)
            StartPipeServer();

            // MainWindow 생성 및 표시
            var mainWindow = new MainWindow();
            mainWindow.Show();

            // 커맨드 라인 인수가 있으면 처리
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            if (args.Length > 0)
            {
                mainWindow.LoadFilesFromArgs(args);
            }
        }

        private static void SendArgsToRunningInstance(string[] args)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(5000); // 5초 대기

                using var writer = new StreamWriter(client) { AutoFlush = true };

                if (args.Length == 0)
                {
                    // 인수 없이 실행된 경우 - 기존 앱 활성화만
                    writer.WriteLine("__ACTIVATE__");
                }
                else
                {
                    foreach (var arg in args)
                    {
                        writer.WriteLine(arg);
                    }
                }
            }
            catch
            {
                // 연결 실패 - 무시
            }
        }

        private void StartPipeServer()
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        await server.WaitForConnectionAsync();

                        using var reader = new StreamReader(server);
                        var files = new List<string>();

                        string? line;
                        while ((line = await reader.ReadLineAsync()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && line != "__ACTIVATE__")
                                files.Add(line);
                        }

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (MainWindow is MainWindow mainWindow)
                            {
                                if (files.Count > 0)
                                {
                                    mainWindow.LoadFilesFromArgs(files.ToArray());
                                }

                                // 창 활성화
                                if (mainWindow.WindowState == WindowState.Minimized)
                                    mainWindow.WindowState = WindowState.Normal;

                                mainWindow.Activate();
                                mainWindow.Topmost = true;
                                mainWindow.Topmost = false;
                                mainWindow.Focus();
                            }
                        });
                    }
                    catch
                    {
                        await Task.Delay(100);
                    }
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                _mutex?.ReleaseMutex();
                _mutex?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}
