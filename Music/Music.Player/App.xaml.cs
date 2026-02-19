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
        private static MainWindow? _mainWindow;

        // 파일 버퍼링용
        private static readonly List<string> _pendingFiles = [];
        private static System.Threading.Timer? _processTimer;
        private static readonly System.Threading.Lock _fileLock = new();

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

            // 첫 번째 인스턴스 - 파이프 서버 먼저 시작
            StartPipeServer();

            // WPF 앱 시작
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 기존 타이머 취소
            _processTimer?.Dispose();
            _processTimer = null;

            // MainWindow 생성 및 표시
            _mainWindow = new MainWindow();
            _mainWindow.Show();

            // 모든 pending files + 명령줄 인수를 한 번에 처리
            var allFiles = new List<string>();
            lock (_fileLock)
            {
                allFiles.AddRange(_pendingFiles);
                _pendingFiles.Clear();
            }

            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            allFiles.AddRange(args);

            if (allFiles.Count > 0)
            {
                // 중복 제거 후 정렬하여 처리
                var uniqueFiles = allFiles.Distinct().OrderBy(f => f).ToArray();
                _mainWindow.LoadFilesFromArgs(uniqueFiles);
            }
        }

        private static void SendArgsToRunningInstance(string[] args)
        {
            // 여러 번 재시도
            for (int retry = 0; retry < 20; retry++)
            {
                try
                {
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(2000);

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
                    return; // 성공
                }
                catch
                {
                    Thread.Sleep(150); // 재시도 전 대기
                }
            }
        }

        private static void StartPipeServer()
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
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            if (!string.IsNullOrWhiteSpace(line) && line != "__ACTIVATE__")
                            {
                                lock (_fileLock)
                                {
                                    _pendingFiles.Add(line);
                                }
                            }
                        }

                        // 타이머 리셋 - 300ms 후에 모은 파일들 처리
                        _processTimer?.Dispose();
                        _processTimer = new System.Threading.Timer(_ => ProcessPendingFiles(), null, 300, Timeout.Infinite);
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
            Thread.Sleep(100);
        }

        private static void ProcessPendingFiles()
        {
            string[] files;
            lock (_fileLock)
            {
                if (_pendingFiles.Count == 0) return;
                files = _pendingFiles.ToArray();
                _pendingFiles.Clear();
            }

            // 파일명 순서로 정렬
            files = files.OrderBy(f => f).ToArray();

            // UI 스레드에서 처리
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                if (_mainWindow != null)
                {
                    if (files.Length > 0)
                    {
                        _mainWindow.LoadFilesFromArgs(files);
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
