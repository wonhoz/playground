using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Windows;

namespace Music.Player
{
    public partial class App : Application
    {
        private const string MutexName = "Local\\MusicPlayer_SingleInstance_Mutex";
        private const string PipeName = "MusicPlayer_Pipe_IPC";
        private static Mutex? _mutex;
        private static bool _ownsMutex;
        public static bool HasCommandLineArgs { get; private set; }
        private static readonly string LogFile = @"C:\Users\admin\Desktop\musicplayer_debug.log";

        private static void Log(string message)
        {
            try
            {
                var line = $"[{DateTime.Now:HH:mm:ss.fff}] [PID:{Environment.ProcessId}] {message}";
                File.AppendAllText(LogFile, line + Environment.NewLine);
            }
            catch { }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            Log($"=== Main started, args: {string.Join(", ", args)} ===");

            HasCommandLineArgs = args.Length > 0;

            bool createdNew;
            _mutex = new Mutex(true, MutexName, out createdNew);
            _ownsMutex = createdNew;

            Log($"Mutex created: {createdNew}, owns: {_ownsMutex}");

            if (!_ownsMutex)
            {
                Log("Secondary instance - sending args to primary");
                // 이미 실행 중 - 파일 경로를 기존 인스턴스에 전달하고 종료
                SendArgsToRunningInstance(args);
                _mutex.Dispose();
                Log("Secondary instance exiting");
                return;
            }

            Log("Primary instance - starting pipe server");
            // 첫 번째 인스턴스 - 파이프 서버 먼저 시작
            StartPipeServerStatic();

            // WPF 앱 시작
            Log("Starting WPF app");
            var app = new App();
            app.InitializeComponent();
            app.Run();
        }

        private static MainWindow? _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("OnStartup: called");
            base.OnStartup(e);

            // 기존 타이머 취소
            _processTimer?.Dispose();
            _processTimer = null;

            // MainWindow 생성 및 표시
            _mainWindow = new MainWindow();
            _mainWindow.Show();
            Log("OnStartup: MainWindow created and shown");

            // 모든 pending files + 명령줄 인수를 한 번에 처리
            var allFiles = new List<string>();
            lock (_fileLock)
            {
                allFiles.AddRange(_pendingFiles);
                _pendingFiles.Clear();
            }

            var args = Environment.GetCommandLineArgs().Skip(1).ToArray();
            allFiles.AddRange(args);

            Log($"OnStartup: total files to process = {allFiles.Count}");
            foreach (var f in allFiles) Log($"  - {f}");

            if (allFiles.Count > 0)
            {
                // 중복 제거 후 정렬하여 처리
                var uniqueFiles = allFiles.Distinct().OrderBy(f => f).ToArray();
                Log($"OnStartup: unique files = {uniqueFiles.Length}");
                _mainWindow.LoadFilesFromArgs(uniqueFiles);
            }

            _initialLoadDone = true;
            Log("OnStartup: initial load done");
        }

        private static void SendArgsToRunningInstance(string[] args)
        {
            Log($"SendArgs: attempting to send {args.Length} args");
            // 여러 번 재시도
            for (int retry = 0; retry < 20; retry++)
            {
                try
                {
                    Log($"SendArgs: retry {retry}, connecting to pipe...");
                    using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
                    client.Connect(2000);
                    Log("SendArgs: connected!");

                    using var writer = new StreamWriter(client) { AutoFlush = true };

                    if (args.Length == 0)
                    {
                        writer.WriteLine("__ACTIVATE__");
                        Log("SendArgs: sent __ACTIVATE__");
                    }
                    else
                    {
                        foreach (var arg in args)
                        {
                            writer.WriteLine(arg);
                            Log($"SendArgs: sent file: {arg}");
                        }
                    }
                    Log("SendArgs: success!");
                    return; // 성공
                }
                catch (Exception ex)
                {
                    Log($"SendArgs: error - {ex.Message}");
                    Thread.Sleep(150); // 재시도 전 대기
                }
            }
            Log("SendArgs: FAILED after all retries");
        }

        private static readonly List<string> _pendingFiles = new();
        private static System.Threading.Timer? _processTimer;
        private static readonly object _fileLock = new();
        private static bool _initialLoadDone;

        private static void StartPipeServerStatic()
        {
            Log("StartPipeServer: starting...");
            var thread = new Thread(() =>
            {
                Log("PipeServer thread started");
                while (true)
                {
                    try
                    {
                        Log("PipeServer: creating new pipe and waiting for connection...");
                        using var server = new NamedPipeServerStream(PipeName, PipeDirection.In);
                        server.WaitForConnection();
                        Log("PipeServer: client connected!");

                        using var reader = new StreamReader(server);
                        string? line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            Log($"PipeServer: received line: {line}");
                            if (!string.IsNullOrWhiteSpace(line) && line != "__ACTIVATE__")
                            {
                                lock (_fileLock)
                                {
                                    _pendingFiles.Add(line);
                                    Log($"PipeServer: added to pending, count: {_pendingFiles.Count}");
                                }
                            }
                        }

                        // 타이머 리셋 - 300ms 후에 모은 파일들 처리
                        Log("PipeServer: scheduling ProcessPendingFiles in 300ms");
                        _processTimer?.Dispose();
                        _processTimer = new System.Threading.Timer(_ => ProcessPendingFiles(), null, 300, Timeout.Infinite);
                    }
                    catch (Exception ex)
                    {
                        Log($"PipeServer: error - {ex.Message}");
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
            Log("StartPipeServer: done");
        }

        private static void ProcessPendingFiles()
        {
            Log("ProcessPendingFiles: called");
            string[] files;
            lock (_fileLock)
            {
                Log($"ProcessPendingFiles: pending count = {_pendingFiles.Count}");
                if (_pendingFiles.Count == 0) return;
                files = _pendingFiles.ToArray();
                _pendingFiles.Clear();
            }

            // 파일명 순서로 정렬
            files = files.OrderBy(f => f).ToArray();

            Log($"ProcessPendingFiles: processing {files.Length} files, initialLoadDone={_initialLoadDone}");
            foreach (var f in files) Log($"  - {f}");

            // UI 스레드에서 처리
            System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
            {
                Log("ProcessPendingFiles: in UI thread");
                if (_mainWindow != null)
                {
                    if (files.Length > 0)
                    {
                        // 항상 플레이리스트 대체
                        Log("ProcessPendingFiles: calling LoadFilesFromArgs");
                        _mainWindow.LoadFilesFromArgs(files);
                    }

                    // 창 활성화
                    if (_mainWindow.WindowState == WindowState.Minimized)
                        _mainWindow.WindowState = WindowState.Normal;

                    _mainWindow.Activate();
                    _mainWindow.Topmost = true;
                    _mainWindow.Topmost = false;
                    _mainWindow.Focus();
                    Log("ProcessPendingFiles: done activating window");
                }
                else
                {
                    Log("ProcessPendingFiles: _mainWindow is NULL!");
                }
            });
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
