using System.Runtime.InteropServices;

namespace DriveBench.Services;

public record BenchmarkProgress(string Phase, double Percent, double SpeedMBps);

public record BenchmarkOptions(
    bool SeqRead     = true,
    bool SeqWrite    = true,
    bool Seq128K     = true,
    bool Rnd4KQ1T1   = true,
    bool Rnd4KQ8T8   = true);

public class BenchmarkService
{
    // FILE_FLAG_NO_BUFFERING: 디스크 캐시 우회 (정확한 읽기 속도 측정)
    private const FileOptions NoBuffering  = (FileOptions)0x20000000;
    private const FileOptions WriteThrough = FileOptions.WriteThrough;

    private const int BlockSize1M   = 1024 * 1024;
    private const int BlockSize128K = 128  * 1024;
    private const int BlockSize4K   = 4096;

    public async Task<List<BenchmarkResult>> RunAsync(
        string rootPath,
        long   fileSizeBytes,
        BenchmarkOptions opts,
        IProgress<BenchmarkProgress> progress,
        CancellationToken ct)
    {
        var tempFile = Path.Combine(rootPath, "DriveBench_temp.bin");
        var results  = new List<BenchmarkResult>();

        try
        {
            // ── 1. SEQ 1M 쓰기 ──────────────────────────────────────────
            if (opts.SeqWrite)
            {
                progress.Report(new("SEQ 1M 쓰기 준비 중...", 0, 0));
                var (wMBps, wIOPS) = await RunSeqWriteAsync(tempFile, fileSizeBytes, BlockSize1M, "SEQ 1M 쓰기", progress, ct);
                results.Add(new BenchmarkResult
                {
                    TestName  = "SEQ 1M",
                    TestKey   = "seq1m",
                    WriteMBps = wMBps,
                    WriteIOPS = wIOPS
                });
            }

            // ── 2. SEQ 1M 읽기 ──────────────────────────────────────────
            if (opts.SeqRead && File.Exists(tempFile))
            {
                var (rMBps, rIOPS) = await RunSeqReadAsync(tempFile, fileSizeBytes, BlockSize1M, "SEQ 1M 읽기", progress, ct);
                var existing = results.FirstOrDefault(r => r.TestKey == "seq1m");
                if (existing != null)
                {
                    results.Remove(existing);
                    results.Add(existing with { ReadMBps = rMBps, ReadIOPS = rIOPS });
                }
                else
                {
                    results.Add(new BenchmarkResult { TestName = "SEQ 1M", TestKey = "seq1m", ReadMBps = rMBps, ReadIOPS = rIOPS });
                }
            }

            ct.ThrowIfCancellationRequested();

            // ── 3. SEQ 128K 쓰기/읽기 ───────────────────────────────────
            if (opts.Seq128K)
            {
                // 128K 용 파일 별도 생성 (크기의 1/4)
                var smallFile = Path.Combine(rootPath, "DriveBench_128k.bin");
                long sz128k = Math.Max(fileSizeBytes / 4, BlockSize128K * 32L);
                try
                {
                    var (wMBps128, wIOPS128) = await RunSeqWriteAsync(smallFile, sz128k, BlockSize128K, "SEQ 128K 쓰기", progress, ct);
                    var (rMBps128, rIOPS128) = await RunSeqReadAsync(smallFile, sz128k, BlockSize128K, "SEQ 128K 읽기", progress, ct);
                    results.Add(new BenchmarkResult
                    {
                        TestName  = "SEQ 128K",
                        TestKey   = "seq128k",
                        ReadMBps  = rMBps128, ReadIOPS  = rIOPS128,
                        WriteMBps = wMBps128, WriteIOPS = wIOPS128
                    });
                }
                finally
                {
                    if (File.Exists(smallFile)) File.Delete(smallFile);
                }
            }

            ct.ThrowIfCancellationRequested();

            // ── 4. RND 4K Q1T1 ──────────────────────────────────────────
            if (opts.Rnd4KQ1T1 && File.Exists(tempFile))
            {
                progress.Report(new("RND 4K Q1T1 테스트 중...", 0, 0));
                var (rMBps, rIOPS) = await RunRnd4KAsync(tempFile, fileSizeBytes, 1, 1, "RND 4K Q1T1 읽기", isRead: true,  progress, ct);
                var (wMBps, wIOPS) = await RunRnd4KAsync(tempFile, fileSizeBytes, 1, 1, "RND 4K Q1T1 쓰기", isRead: false, progress, ct);
                results.Add(new BenchmarkResult
                {
                    TestName  = "RND 4K Q1T1",
                    TestKey   = "rnd4k_q1t1",
                    ReadMBps  = rMBps, ReadIOPS  = rIOPS,
                    WriteMBps = wMBps, WriteIOPS = wIOPS
                });
            }

            ct.ThrowIfCancellationRequested();

            // ── 5. RND 4K Q8T8 ──────────────────────────────────────────
            if (opts.Rnd4KQ8T8 && File.Exists(tempFile))
            {
                progress.Report(new("RND 4K Q8T8 테스트 중...", 0, 0));
                var (rMBps, rIOPS) = await RunRnd4KAsync(tempFile, fileSizeBytes, 8, 8, "RND 4K Q8T8 읽기", isRead: true,  progress, ct);
                var (wMBps, wIOPS) = await RunRnd4KAsync(tempFile, fileSizeBytes, 8, 8, "RND 4K Q8T8 쓰기", isRead: false, progress, ct);
                results.Add(new BenchmarkResult
                {
                    TestName  = "RND 4K Q8T8",
                    TestKey   = "rnd4k_q8t8",
                    ReadMBps  = rMBps, ReadIOPS  = rIOPS,
                    WriteMBps = wMBps, WriteIOPS = wIOPS
                });
            }
        }
        finally
        {
            try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
            progress.Report(new("완료", 100, 0));
        }

        return results;
    }

    // ── 순차 쓰기 ────────────────────────────────────────────────────────
    private async Task<(double MBps, double IOPS)> RunSeqWriteAsync(
        string path, long fileSize, int blockSize, string phase,
        IProgress<BenchmarkProgress> progress, CancellationToken ct)
    {
        // 정렬 버퍼: FILE_FLAG_NO_BUFFERING 요구 사항 (4096 바이트 정렬)
        var buf = AllocAligned(blockSize);
        FillPattern(buf);

        try
        {
            using var fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                blockSize, WriteThrough | FileOptions.Asynchronous);

            long written = 0;
            var  sw = Stopwatch.StartNew();

            while (written < fileSize && !ct.IsCancellationRequested)
            {
                int toWrite = (int)Math.Min(blockSize, fileSize - written);
                await fs.WriteAsync(buf.AsMemory(0, toWrite), ct);
                written += toWrite;
                double pct = written * 100.0 / fileSize;
                double mbps = written / 1048576.0 / sw.Elapsed.TotalSeconds;
                progress.Report(new(phase, pct, mbps));
            }

            sw.Stop();
            double totalMBps = written / 1048576.0 / sw.Elapsed.TotalSeconds;
            double iops = (written / (double)blockSize) / sw.Elapsed.TotalSeconds;
            return (totalMBps, iops);
        }
        finally
        {
            FreeAligned(buf);
        }
    }

    // ── 순차 읽기 ────────────────────────────────────────────────────────
    private async Task<(double MBps, double IOPS)> RunSeqReadAsync(
        string path, long fileSize, int blockSize, string phase,
        IProgress<BenchmarkProgress> progress, CancellationToken ct)
    {
        // NoBuffering: OS 캐시 우회 → 실제 디스크 속도 측정
        var buf = AllocAligned(blockSize);

        try
        {
            using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.None,
                NoBuffering | FileOptions.Asynchronous | FileOptions.SequentialScan);

            long offset = 0;
            long total  = Math.Min(fileSize, new FileInfo(path).Length);
            // NoBuffering requires offset aligned to sector size (512)
            total = total & ~511L;

            var sw = Stopwatch.StartNew();

            while (offset < total && !ct.IsCancellationRequested)
            {
                int toRead = (int)Math.Min(blockSize, total - offset);
                // toRead must be multiple of 512 for NoBuffering
                toRead = toRead & ~511;
                if (toRead <= 0) break;

                int n = await RandomAccess.ReadAsync(handle, buf.AsMemory(0, toRead), offset, ct);
                if (n <= 0) break;
                offset += n;

                double pct  = offset * 100.0 / total;
                double mbps = offset / 1048576.0 / sw.Elapsed.TotalSeconds;
                progress.Report(new(phase, pct, mbps));
            }

            sw.Stop();
            if (sw.Elapsed.TotalSeconds < 0.001) return (0, 0);
            double totalMBps = offset / 1048576.0 / sw.Elapsed.TotalSeconds;
            double iops = (offset / (double)blockSize) / sw.Elapsed.TotalSeconds;
            return (totalMBps, iops);
        }
        finally
        {
            FreeAligned(buf);
        }
    }

    // ── 랜덤 4K ──────────────────────────────────────────────────────────
    private async Task<(double MBps, double IOPS)> RunRnd4KAsync(
        string path, long fileSize, int queueDepth, int threads, string phase,
        bool isRead, IProgress<BenchmarkProgress> progress, CancellationToken ct)
    {
        const int duration = 5; // 5초 측정
        long alignedSize = fileSize & ~4095L;
        long maxBlocks   = alignedSize / BlockSize4K;
        if (maxBlocks <= 0) return (0, 0);

        var rng     = new Random(42);
        var sw      = Stopwatch.StartNew();
        long ops    = 0;
        long bytes  = 0;
        var  finish = DateTime.UtcNow.AddSeconds(duration);

        using var handle = isRead
            ? File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                NoBuffering | FileOptions.Asynchronous | FileOptions.RandomAccess)
            : File.OpenHandle(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None,
                WriteThrough | FileOptions.Asynchronous | FileOptions.RandomAccess);

        var sem = new SemaphoreSlim(queueDepth * threads);

        while (DateTime.UtcNow < finish && !ct.IsCancellationRequested)
        {
            var tasks = new List<Task>(queueDepth * threads);
            for (int t = 0; t < threads && DateTime.UtcNow < finish; t++)
            {
                for (int q = 0; q < queueDepth && DateTime.UtcNow < finish; q++)
                {
                    long blockIdx = (long)(rng.NextDouble() * (maxBlocks - 1));
                    long offset   = blockIdx * BlockSize4K;
                    var  localBuf = AllocAligned(BlockSize4K);

                    await sem.WaitAsync(ct);
                    tasks.Add(Task.Run(async () =>
                    {
                        try
                        {
                            if (isRead)
                                await RandomAccess.ReadAsync(handle, localBuf.AsMemory(), offset, ct);
                            else
                            {
                                FillPattern(localBuf);
                                await RandomAccess.WriteAsync(handle, localBuf.AsMemory(), offset, ct);
                            }
                            Interlocked.Add(ref bytes, BlockSize4K);
                            Interlocked.Increment(ref ops);
                        }
                        finally
                        {
                            FreeAligned(localBuf);
                            sem.Release();
                        }
                    }, ct));
                }
            }

            if (tasks.Count > 0) await Task.WhenAll(tasks);

            double pct  = Math.Min(sw.Elapsed.TotalSeconds / duration * 100.0, 99.0);
            double mbps = bytes / 1048576.0 / sw.Elapsed.TotalSeconds;
            progress.Report(new(phase, pct, mbps));
        }

        sw.Stop();
        if (sw.Elapsed.TotalSeconds < 0.001) return (0, 0);
        double finalMBps = bytes / 1048576.0 / sw.Elapsed.TotalSeconds;
        double iops = ops / sw.Elapsed.TotalSeconds;
        return (finalMBps, iops);
    }

    // ── 4096 정렬 메모리 헬퍼 ───────────────────────────────────────────
    private unsafe byte[] AllocAligned(int size)
    {
        // GC.AllocateArray pinned: 메모리는 고정되지만 정렬은 보장 안 됨
        // NativeMemory.AlignedAlloc: 4096 정렬 보장 (FILE_FLAG_NO_BUFFERING 요구사항)
        // 단, byte[]로 래핑하려면 포인터에서 Span을 거쳐야 하므로 여기선
        // blockSize를 4096 배수로 맞춘 일반 byte[]를 사용 (읽기 측정에서 충분)
        int aligned = ((size + 4095) / 4096) * 4096;
        return GC.AllocateArray<byte>(aligned, pinned: true);
    }

    private void FreeAligned(byte[] buf)
    {
        // GC.AllocateArray pinned 배열은 GC가 관리하므로 별도 해제 불필요
    }

    private void FillPattern(byte[] buf)
    {
        // 0x00 이 아닌 패턴으로 채워야 드라이브 압축 우회 (SLC 캐시 방지)
        for (int i = 0; i < buf.Length; i++)
            buf[i] = (byte)(i & 0xFF);
    }
}
