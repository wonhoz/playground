using System.IO;
using System.IO.Pipes;
using System.Windows;

namespace Music.Player
{
    public partial class App : Application
    {
        private const string MutexName = "MusicPlayer_SingleInstance_A1B2C3D4";
        private const string PipeName = "MusicPlayer_Pipe_A1B2C3D4";
        private static Mutex? _mutex;
        private static bool _ownsMutex;

        [STAThread]
        public static void Main(string[] args)
        {
            _mutex = new Mutex(false, MutexName);

            try
            {
                // Mutex 획득 시도 (최대 100ms 대기)
                _ownsMutex = _mutex.WaitOne(100, false);
            }
            catch (AbandonedMutexException)
            {
                // 이전 프로세스가 비정상 종료한 경우 - Mutex 소유권 획득됨
                _ownsMutex = true;
            }

            if (!_ownsMutex)
            {
                // 이미 실행 중 - 파일 경로를 기존 인스턴스에 전달하고 종료
                SendArgsToRunningInstance(args);
                _mutex.Dispose();
                return;
            }

            // 첫 번째 인스턴스 - 파이프 서버 먼저 시작
            StartPipeServerStatic();

            // WPF 앱 시작
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // MainWindow 생성 및 표시
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // 커맨드 라인 인수가 있으면 처리
            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            if (args.Length > 0)
            {
                _mainWindow.LoadFilesFromArgs(args);
            }
        }

        private static void SendArgsToRunningInstance(string[] args)
        {
            // 여러 번 재시도
            for (int retry = 0; retry < 10; retry++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(1000);

                    using var writer = new StreamWriter(client) { AutoFlush = true };

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
                    return; // 성공
                }
                catch
                {
                    Thread.Sleep(100); // 재시도 전 대기
                }
            }
        }

        private static void StartPipeServerStatic()
        {
            var thread = new Thread(() =>
            {
                while (true)
                {
                    try
                    {
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        server.WaitForConnection();

                        using var reader = new StreamReader(server);
                        var files = new List<string>();

                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && line != "__ACTIVATE__")
                                files.Add(line);
                        }

                        // UI 스레드에서 처리
                        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
                        {
                            if (_mainWindow != null)
                            {
                                if (files.Count > 0)
                                {
                                    _mainWindow.LoadFilesFromArgs(files.ToArray());
                                }

                                // 창 활성화
                                if (_mainWindow.WindowState == WindowState.Minimized)
                                    _mainWindow.WindowState = WindowState.Normal;

                                _mainWindow.Activate();
                                _mainWindow.Topmost = true;
                                _mainWindow.Topmost = false;
                                _mainWindow.Focus();
                            }
                        });
                    }
                    catch
                    {
                        Thread.Sleep(50);
                    }
                }
            })
            {
                IsBackground = true,
                Name = "PipeServer"
            };
            thread.Start();

            // 파이프 서버가 시작될 때까지 잠시 대기
            Thread.Sleep(50);
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_ownsMutex && _mutex != null)
            {
                try
                {
                    _mutex.ReleaseMutex();
                }
                catch { }
            }
            _mutex?.Dispose();

            base.OnExit(e);
        }
    }
}
