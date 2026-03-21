using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Controls;

namespace ProcBench;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    private CancellationTokenSource? _cts;
    private BenchResult _lastResult = new();
    private string _tempFile = "";

    public MainWindow()
    {
        InitializeComponent();
        var handle = new System.Windows.Interop.WindowInteropHelper(this).EnsureHandle();
        int v = 1;
        DwmSetWindowAttribute(handle, 20, ref v, sizeof(int));
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        LoadSystemInfo();
        _tempFile = Path.Combine(Path.GetTempPath(), "procbench_tmp.dat");
    }

    private void LoadSystemInfo()
    {
        try
        {
            // CPU 이름
            using var searcher = new ManagementObjectSearcher("SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
            foreach (ManagementObject obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                var cores = obj["NumberOfCores"]?.ToString() ?? "?";
                var logical = obj["NumberOfLogicalProcessors"]?.ToString() ?? "?";
                var shortName = name.Length > 40 ? name[..40] + "…" : name;
                LblCpuName.Text = $"{shortName} | {cores}C/{logical}T";
                break;
            }
        }
        catch { LblCpuName.Text = "CPU 정보 없음"; }

        // RAM
        var totalMb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1024 / 1024;
        LblRamTotal.Text = $"RAM {totalMb / 1024:F1} GB";

        // 드라이브
        var drive = DriveInfo.GetDrives().FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);
        LblDiskName.Text = drive != null ? $"{drive.Name} ({drive.DriveFormat}) {drive.TotalSize / 1024 / 1024 / 1024} GB" : "드라이브 없음";

        SetStatus("시스템 정보 로드 완료. 벤치마크를 실행하세요.");
    }

    private async void BtnRunAll_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetBusy(true);
        try
        {
            await RunCpuBenchAsync(_cts.Token);
            await RunMemBenchAsync(_cts.Token);
            await RunDiskBenchAsync(_cts.Token);
            PbGlobal.Value = 100;
            SetStatus($"전체 벤치 완료 — CPU:{_lastResult.CpuScore} MEM:{_lastResult.MemScore} DISK:{_lastResult.DiskScore}");
        }
        catch (OperationCanceledException) { SetStatus("벤치마크 중지됨"); }
        finally { SetBusy(false); CleanupTemp(); }
    }

    private async void BtnRunCpu_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetBusy(true);
        try { await RunCpuBenchAsync(_cts.Token); }
        catch (OperationCanceledException) { SetStatus("중지됨"); }
        finally { SetBusy(false); }
    }

    private async void BtnRunMem_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetBusy(true);
        try { await RunMemBenchAsync(_cts.Token); }
        catch (OperationCanceledException) { SetStatus("중지됨"); }
        finally { SetBusy(false); }
    }

    private async void BtnRunDisk_Click(object sender, RoutedEventArgs e)
    {
        _cts = new CancellationTokenSource();
        SetBusy(true);
        try { await RunDiskBenchAsync(_cts.Token); }
        catch (OperationCanceledException) { SetStatus("중지됨"); }
        finally { SetBusy(false); CleanupTemp(); }
    }

    private void BtnStop_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    // ── CPU 벤치 ────────────────────────────────────────────
    private async Task RunCpuBenchAsync(CancellationToken ct)
    {
        SetStatus("CPU 벤치마크 실행 중...");

        // 정수 연산 (단일 스레드 소수 계산)
        var intOps = await Task.Run(() => BenchCpuInt(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblCpuInt.Text = $"{intOps:N0} 소수/초";
            PbCpuInt.Value = Math.Min(100, intOps / 5000.0);
        });
        UpdateProgress(10);

        // 부동소수점
        var floatOps = await Task.Run(() => BenchCpuFloat(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblCpuFloat.Text = $"{floatOps / 1e6:F1} MFLOPS";
            PbCpuFloat.Value = Math.Min(100, floatOps / 1e8 * 100);
        });
        UpdateProgress(20);

        // 멀티스레드
        var multiOps = await Task.Run(() => BenchCpuMulti(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblCpuMulti.Text = $"{multiOps:N0} 소수/초 (전체 코어)";
            PbCpuMulti.Value = Math.Min(100, multiOps / (5000.0 * Environment.ProcessorCount));
        });
        UpdateProgress(30);

        // AES 암호화
        var aesRate = await Task.Run(() => BenchCpuAes(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblCpuCrypto.Text = $"{aesRate:F0} MB/초";
            PbCpuCrypto.Value = Math.Min(100, aesRate / 20.0);
        });
        UpdateProgress(40);

        // 종합 점수
        var score = (int)((intOps / 100.0) + (floatOps / 1e6) + (multiOps / 200.0) + (aesRate * 2));
        _lastResult.CpuScore = score;
        Dispatcher.Invoke(() =>
        {
            LblCpuScore.Text = score.ToString("N0");
            SetStatus($"CPU 벤치 완료 — {score:N0}점");
        });
    }

    private static long BenchCpuInt(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        long count = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            // 소수 계산 (2~100000)
            for (int n = 2; n < 100000; n++)
            {
                bool isPrime = true;
                for (int i = 2; i * i <= n; i++)
                    if (n % i == 0) { isPrime = false; break; }
                if (isPrime) count++;
            }
        }
        return count * 1000 / sw.ElapsedMilliseconds;
    }

    private static double BenchCpuFloat(CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        double sum = 0;
        long ops = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            for (int i = 0; i < 10000; i++)
            {
                sum += Math.Sin(i) * Math.Cos(i) * Math.Sqrt(i + 1);
                ops++;
            }
        }
        _ = sum; // prevent optimization
        return ops * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static long BenchCpuMulti(CancellationToken ct)
    {
        var threads = Environment.ProcessorCount;
        long totalCount = 0;
        var tasks = Enumerable.Range(0, threads).Select(_ => Task.Run(() =>
        {
            var sw = Stopwatch.StartNew();
            long c = 0;
            while (sw.ElapsedMilliseconds < 2000)
            {
                if (ct.IsCancellationRequested) break;
                for (int n = 2; n < 50000; n++)
                {
                    bool isPrime = true;
                    for (int i = 2; i * i <= n; i++)
                        if (n % i == 0) { isPrime = false; break; }
                    if (isPrime) c++;
                }
            }
            Interlocked.Add(ref totalCount, c * 1000 / Math.Max(1, sw.ElapsedMilliseconds));
        })).ToArray();
        Task.WaitAll(tasks);
        return totalCount;
    }

    private static double BenchCpuAes(CancellationToken ct)
    {
        using var aes = Aes.Create();
        aes.KeySize = 256;
        aes.GenerateKey();
        aes.GenerateIV();
        var data = new byte[1024 * 1024]; // 1MB
        new Random(42).NextBytes(data);
        var output = new byte[data.Length + 64];

        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            using var enc = aes.CreateEncryptor();
            enc.TransformBlock(data, 0, data.Length, output, 0);
            totalBytes += data.Length;
        }
        return totalBytes / (1024.0 * 1024.0) * 1000.0 / sw.ElapsedMilliseconds;
    }

    // ── 메모리 벤치 ──────────────────────────────────────────
    private async Task RunMemBenchAsync(CancellationToken ct)
    {
        SetStatus("메모리 벤치마크 실행 중...");
        const int sizeBytes = 256 * 1024 * 1024; // 256 MB

        // 순차 읽기
        var readGbs = await Task.Run(() => BenchMemRead(sizeBytes, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblMemRead.Text = $"{readGbs:F2} GB/초";
            PbMemRead.Value = Math.Min(100, readGbs / 50.0 * 100);
        });
        UpdateProgress(50);

        // 순차 쓰기
        var writeGbs = await Task.Run(() => BenchMemWrite(sizeBytes, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblMemWrite.Text = $"{writeGbs:F2} GB/초";
            PbMemWrite.Value = Math.Min(100, writeGbs / 40.0 * 100);
        });
        UpdateProgress(60);

        // 랜덤 접근
        var randGbs = await Task.Run(() => BenchMemRandom(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblMemRandom.Text = $"{randGbs:F2} GB/초";
            PbMemRandom.Value = Math.Min(100, randGbs / 10.0 * 100);
        });
        UpdateProgress(65);

        // 메모리 지연 (나노초)
        var latNs = await Task.Run(() => BenchMemLatency(ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblMemLatency.Text = $"{latNs:F1} ns";
            PbMemLatency.Value = Math.Max(0, Math.Min(100, (200.0 - latNs) / 2));
        });
        UpdateProgress(70);

        var score = (int)(readGbs * 100 + writeGbs * 80 + randGbs * 200 - latNs * 2);
        score = Math.Max(0, score);
        _lastResult.MemScore = score;
        Dispatcher.Invoke(() =>
        {
            LblMemScore.Text = score.ToString("N0");
            SetStatus($"메모리 벤치 완료 — {score:N0}점");
        });
    }

    private static unsafe double BenchMemRead(int sizeBytes, CancellationToken ct)
    {
        var buf = new byte[sizeBytes];
        new Random(1).NextBytes(buf);
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        long sum = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            fixed (byte* p = buf)
            {
                long* lp = (long*)p;
                int count = sizeBytes / 8;
                for (int i = 0; i < count; i++) sum += lp[i];
            }
            totalBytes += sizeBytes;
        }
        _ = sum;
        return totalBytes / (1024.0 * 1024 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static unsafe double BenchMemWrite(int sizeBytes, CancellationToken ct)
    {
        var buf = new byte[sizeBytes];
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            fixed (byte* p = buf)
            {
                long* lp = (long*)p;
                int count = sizeBytes / 8;
                for (int i = 0; i < count; i++) lp[i] = i * 7;
            }
            totalBytes += sizeBytes;
        }
        return totalBytes / (1024.0 * 1024 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static double BenchMemRandom(CancellationToken ct)
    {
        const int size = 64 * 1024 * 1024; // 64 MB
        var buf = new int[size / 4];
        var rng = new Random(42);
        var indices = Enumerable.Range(0, 1_000_000).Select(_ => rng.Next(buf.Length)).ToArray();

        var sw = Stopwatch.StartNew();
        long totalAccesses = 0;
        long sum = 0;
        while (sw.ElapsedMilliseconds < 2000)
        {
            ct.ThrowIfCancellationRequested();
            foreach (var idx in indices) sum += buf[idx];
            totalAccesses += indices.Length;
        }
        _ = sum;
        return totalAccesses * 4L / (1024.0 * 1024 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static double BenchMemLatency(CancellationToken ct)
    {
        // 포인터 체이싱으로 캐시 레이턴시 측정
        const int size = 256 * 1024 * 1024 / 4; // 256 MB / 4 bytes
        var buf = new int[size];
        // 랜덤 순열 생성
        var rng = new Random(123);
        var perm = Enumerable.Range(0, size).OrderBy(_ => rng.Next()).ToArray();
        for (int i = 0; i < size; i++) buf[i] = perm[i];

        var sw = Stopwatch.StartNew();
        int steps = 10_000_000;
        int pos = 0;
        for (int i = 0; i < steps; i++) pos = buf[pos];
        _ = pos;
        var elapsed = sw.Elapsed.TotalNanoseconds;
        return elapsed / steps;
    }

    // ── 스토리지 벤치 ─────────────────────────────────────────
    private async Task RunDiskBenchAsync(CancellationToken ct)
    {
        SetStatus("스토리지 벤치마크 실행 중...");
        const int fileSizeMb = 512;

        // 순차 쓰기
        var seqWriteMbs = await Task.Run(() => BenchDiskSeqWrite(_tempFile, fileSizeMb, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblDiskSeqWrite.Text = $"{seqWriteMbs:F0} MB/초";
            PbDiskSeqWrite.Value = Math.Min(100, seqWriteMbs / 50.0);
        });
        UpdateProgress(80);

        // 순차 읽기
        var seqReadMbs = await Task.Run(() => BenchDiskSeqRead(_tempFile, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblDiskSeqRead.Text = $"{seqReadMbs:F0} MB/초";
            PbDiskSeqRead.Value = Math.Min(100, seqReadMbs / 60.0);
        });
        UpdateProgress(87);

        // 랜덤 4K 읽기
        var randReadMbs = await Task.Run(() => BenchDiskRandRead(_tempFile, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblDiskRandRead.Text = $"{randReadMbs:F2} MB/초";
            PbDiskRandRead.Value = Math.Min(100, randReadMbs / 2.0);
        });
        UpdateProgress(94);

        // 랜덤 4K 쓰기
        var randWriteMbs = await Task.Run(() => BenchDiskRandWrite(_tempFile, ct), ct);
        ct.ThrowIfCancellationRequested();
        Dispatcher.Invoke(() =>
        {
            LblDiskRandWrite.Text = $"{randWriteMbs:F2} MB/초";
            PbDiskRandWrite.Value = Math.Min(100, randWriteMbs / 2.0);
        });
        UpdateProgress(100);

        var score = (int)(seqReadMbs * 0.3 + seqWriteMbs * 0.3 + randReadMbs * 2 + randWriteMbs * 2);
        _lastResult.DiskScore = score;
        Dispatcher.Invoke(() =>
        {
            LblDiskScore.Text = score.ToString("N0");
            SetStatus($"스토리지 벤치 완료 — {score:N0}점");
        });
    }

    private static double BenchDiskSeqWrite(string path, int sizeMb, CancellationToken ct)
    {
        var data = new byte[1024 * 1024]; // 1 MB 청크
        new Random(7).NextBytes(data);
        var sw = Stopwatch.StartNew();
        using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
            bufferSize: 1024 * 1024, FileOptions.WriteThrough);
        for (int i = 0; i < sizeMb; i++)
        {
            ct.ThrowIfCancellationRequested();
            fs.Write(data, 0, data.Length);
        }
        fs.Flush(true);
        return sizeMb * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static double BenchDiskSeqRead(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return 0;
        var buf = new byte[1024 * 1024];
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 1024 * 1024);
        int read;
        while ((read = fs.Read(buf, 0, buf.Length)) > 0)
        {
            ct.ThrowIfCancellationRequested();
            totalBytes += read;
        }
        return totalBytes / (1024.0 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static double BenchDiskRandRead(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return 0;
        var fileSize = new FileInfo(path).Length;
        var buf = new byte[4096]; // 4K
        var rng = new Random(99);
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: 4096, FileOptions.RandomAccess);
        while (sw.ElapsedMilliseconds < 3000)
        {
            ct.ThrowIfCancellationRequested();
            var pos = (long)(rng.NextDouble() * (fileSize - 4096));
            pos = pos / 4096 * 4096;
            fs.Seek(pos, SeekOrigin.Begin);
            fs.ReadExactly(buf, 0, 4096);
            totalBytes += 4096;
        }
        return totalBytes / (1024.0 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private static double BenchDiskRandWrite(string path, CancellationToken ct)
    {
        if (!File.Exists(path)) return 0;
        var fileSize = new FileInfo(path).Length;
        var buf = new byte[4096];
        var rng = new Random(11);
        var sw = Stopwatch.StartNew();
        long totalBytes = 0;
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.None,
            bufferSize: 4096, FileOptions.WriteThrough);
        while (sw.ElapsedMilliseconds < 3000)
        {
            ct.ThrowIfCancellationRequested();
            var pos = (long)(rng.NextDouble() * (fileSize - 4096));
            pos = pos / 4096 * 4096;
            fs.Seek(pos, SeekOrigin.Begin);
            fs.Write(buf, 0, 4096);
            totalBytes += 4096;
        }
        fs.Flush(true);
        return totalBytes / (1024.0 * 1024) * 1000.0 / sw.ElapsedMilliseconds;
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_lastResult.CpuScore == 0 && _lastResult.MemScore == 0 && _lastResult.DiskScore == 0)
        {
            SetStatus("먼저 벤치마크를 실행하세요.");
            return;
        }
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "텍스트 파일|*.txt|JSON 파일|*.json",
            FileName = $"ProcBench_{DateTime.Now:yyyyMMdd_HHmmss}"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("=== Proc.Bench 결과 ===");
        sb.AppendLine($"날짜: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine($"CPU 점수: {_lastResult.CpuScore:N0}");
        sb.AppendLine($"  정수 연산: {LblCpuInt.Text}");
        sb.AppendLine($"  부동소수점: {LblCpuFloat.Text}");
        sb.AppendLine($"  멀티스레드: {LblCpuMulti.Text}");
        sb.AppendLine($"  AES 암호화: {LblCpuCrypto.Text}");
        sb.AppendLine($"메모리 점수: {_lastResult.MemScore:N0}");
        sb.AppendLine($"  순차 읽기: {LblMemRead.Text}");
        sb.AppendLine($"  순차 쓰기: {LblMemWrite.Text}");
        sb.AppendLine($"  랜덤 접근: {LblMemRandom.Text}");
        sb.AppendLine($"  메모리 지연: {LblMemLatency.Text}");
        sb.AppendLine($"스토리지 점수: {_lastResult.DiskScore:N0}");
        sb.AppendLine($"  순차 읽기: {LblDiskSeqRead.Text}");
        sb.AppendLine($"  순차 쓰기: {LblDiskSeqWrite.Text}");
        sb.AppendLine($"  랜덤 4K 읽기: {LblDiskRandRead.Text}");
        sb.AppendLine($"  랜덤 4K 쓰기: {LblDiskRandWrite.Text}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        SetStatus($"결과 저장 완료: {dlg.FileName}");
    }

    private void UpdateProgress(int value) => Dispatcher.Invoke(() => PbGlobal.Value = value);
    private void SetStatus(string msg) => Dispatcher.Invoke(() => StatusBar.Text = msg);

    private void SetBusy(bool busy)
    {
        BtnRunAll.IsEnabled = !busy;
        BtnRunCpu.IsEnabled = !busy;
        BtnRunMem.IsEnabled = !busy;
        BtnRunDisk.IsEnabled = !busy;
        BtnStop.IsEnabled = busy;
        if (!busy) PbGlobal.Value = 0;
    }

    private void CleanupTemp()
    {
        try { if (File.Exists(_tempFile)) File.Delete(_tempFile); } catch { }
    }
}

public class BenchResult
{
    public int CpuScore { get; set; }
    public int MemScore { get; set; }
    public int DiskScore { get; set; }
}
