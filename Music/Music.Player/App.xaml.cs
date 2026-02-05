using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace Music.Player
{
    public partial class App : Application
    {
        private const string MutexName = "MusicPlayer_SingleInstance_Mutex";
        private const string PipeName = "MusicPlayer_Pipe";
        private static Mutex? _mutex;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 단일 인스턴스 체크
            _mutex = new Mutex(true, MutexName, out bool isNewInstance);

            if (!isNewInstance)
            {
                // 이미 실행 중인 인스턴스에 파일 경로 전달
                SendArgsToRunningInstance(e.Args);
                Shutdown();
                return;
            }

            // 파이프 서버 시작 (다른 인스턴스로부터 파일 경로 수신)
            StartPipeServer();

            base.OnStartup(e);

            // MainWindow에 커맨드 라인 인수 전달
            var mainWindow = new MainWindow();
            mainWindow.Show();

            if (e.Args.Length > 0)
            {
                mainWindow.LoadFilesFromArgs(e.Args);
            }
        }

        private void SendArgsToRunningInstance(string[] args)
        {
            if (args.Length == 0) return;

            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(1000);

                using var writer = new StreamWriter(client);
                foreach (var arg in args)
                {
                    writer.WriteLine(arg);
                }
            }
            catch
            {
                // 연결 실패 시 무시
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
                            if (!string.IsNullOrWhiteSpace(line))
                                files.Add(line);
                        }

                        if (files.Count > 0)
                        {
                            Dispatcher.Invoke(() =>
                            {
                                if (MainWindow is MainWindow mainWindow)
                                {
                                    mainWindow.LoadFilesFromArgs(files.ToArray());
                                    mainWindow.Activate();
                                }
                            });
                        }
                    }
                    catch
                    {
                        // 에러 발생 시 재시도
                        await Task.Delay(100);
                    }
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            _mutex?.ReleaseMutex();
            _mutex?.Dispose();
            base.OnExit(e);
        }
    }
}
