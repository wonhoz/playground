using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace Music.Player
{
    public partial class App : Application
    {
        private const string MutexName = "Global\\MusicPlayer_SingleInstance_Mutex";
        private const string PipeName = "MusicPlayer_Pipe";
        private static Mutex? _mutex;
        private static bool _isFirstInstance;

        protected override void OnStartup(StartupEventArgs e)
        {
            // 단일 인스턴스 체크 - Global prefix로 세션 간 공유
            bool createdNew;
            try
            {
                _mutex = new Mutex(true, MutexName, out createdNew);
                _isFirstInstance = createdNew;
            }
            catch (Exception)
            {
                // Mutex 생성 실패 시 (권한 문제 등) - 그냥 실행
                _isFirstInstance = true;
            }

            if (!_isFirstInstance)
            {
                // 이미 실행 중인 인스턴스에 파일 경로 전달
                SendArgsToRunningInstance(e.Args);
                _mutex?.Dispose();
                _mutex = null;
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
            try
            {
                using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                client.Connect(3000); // 3초 대기

                using var writer = new StreamWriter(client) { AutoFlush = true };

                // 인수가 없어도 빈 메시지 전송하여 기존 앱 활성화
                if (args.Length == 0)
                {
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
                            if (!string.IsNullOrWhiteSpace(line) && line != "__ACTIVATE__")
                                files.Add(line);
                        }

                        Dispatcher.Invoke(() =>
                        {
                            if (MainWindow is MainWindow mainWindow)
                            {
                                if (files.Count > 0)
                                {
                                    mainWindow.LoadFilesFromArgs(files.ToArray());
                                }
                                // 항상 창 활성화
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
                        // 에러 발생 시 재시도
                        await Task.Delay(100);
                    }
                }
            });
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_isFirstInstance && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
                _mutex.Dispose();
            }
            base.OnExit(e);
        }
    }
}
