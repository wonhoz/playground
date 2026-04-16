using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace ScreenRecorder.Services;

public static class EncoderService
{
    /// <summary>FFmpeg가 설치되어 있는지 확인</summary>
    public static bool IsFfmpegAvailable() => FindFfmpeg() is not null;

    /// <summary>프레임 시퀀스를 FFmpeg로 MP4 인코딩</summary>
    public static async Task EncodeToMp4Async(List<string> framePaths, int fps, string outputPath,
        bool recordAudio = false, string audioDevice = "")
    {
        if (framePaths.Count == 0) return;

        var ffmpegPath = FindFfmpeg();
        if (ffmpegPath is null)
            throw new InvalidOperationException(
                "FFmpeg를 찾을 수 없습니다.\n\n" +
                "다음 중 하나로 설치해주세요:\n" +
                "  • winget install Gyan.FFmpeg\n" +
                "  • choco install ffmpeg\n" +
                "  • https://ffmpeg.org/download.html");

        // 프레임이 이미 frame_000000.png 연번으로 저장된 폴더를 직접 사용
        var inputDir = Path.GetDirectoryName(framePaths[0])!;
        var inputPattern = Path.Combine(inputDir, "frame_%06d.png");

        // 오디오 입력 옵션 (wasapi로 시스템 오디오 캡처)
        var audioInput = "";
        var audioOutput = "";
        if (recordAudio)
        {
            var device = string.IsNullOrWhiteSpace(audioDevice)
                ? "audio=virtual-audio-capturer"   // VB-Audio 등 가상장치 fallback
                : $"audio={audioDevice}";
            // wasapi loopback: 시스템 오디오 캡처 (재생 중인 오디오)
            audioInput = $"-f dshow -i \"{device}\" ";
            audioOutput = "-c:a aac -b:a 128k ";
        }

        // FFmpeg 인코딩: H.264, yuv420p (호환성), CRF 23 (적정 품질)
        var args = $"-framerate {fps} -i \"{inputPattern}\" {audioInput}" +
                   $"-c:v libx264 -pix_fmt yuv420p -crf 23 -preset fast {audioOutput}" +
                   $"-movflags +faststart -y \"{outputPath}\"";

        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg 프로세스를 시작할 수 없습니다.");
        var stderr = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg 인코딩 실패 (exit {process.ExitCode}):\n{stderr[..Math.Min(stderr.Length, 500)]}");
    }

    /// <summary>프레임 시퀀스를 GIF로 인코딩 (네이티브 System.Drawing)</summary>
    public static async Task EncodeToGifAsync(List<string> framePaths, int fps, string outputPath)
    {
        if (framePaths.Count == 0) return;

        // FFmpeg가 있으면 그걸로 고품질 GIF 생성
        var ffmpegPath = FindFfmpeg();
        if (ffmpegPath is not null)
        {
            await EncodeGifWithFfmpegAsync(ffmpegPath, framePaths, fps, outputPath);
            return;
        }

        // FFmpeg 없으면 네이티브 GIF 인코딩
        await Task.Run(() => EncodeGifNative(framePaths, fps, outputPath));
    }

    private static async Task EncodeGifWithFfmpegAsync(string ffmpegPath, List<string> framePaths, int fps, string outputPath)
    {
        // 프레임이 이미 frame_000000.png 연번으로 저장된 폴더를 직접 사용
        var inputDir = Path.GetDirectoryName(framePaths[0])!;
        var inputPattern = Path.Combine(inputDir, "frame_%06d.png");
        var palettePath = Path.Combine(inputDir, "palette.png");

        try
        {
            // 2-pass: 팔레트 생성 → GIF 인코딩 (고품질)
            var paletteArgs = $"-framerate {fps} -i \"{inputPattern}\" " +
                              $"-vf \"fps={fps},palettegen=stats_mode=diff\" -y \"{palettePath}\"";

            var gifArgs = $"-framerate {fps} -i \"{inputPattern}\" -i \"{palettePath}\" " +
                          $"-lavfi \"fps={fps},paletteuse=dither=bayer:bayer_scale=5\" -y \"{outputPath}\"";

            await RunFfmpegAsync(ffmpegPath, paletteArgs);
            await RunFfmpegAsync(ffmpegPath, gifArgs);
        }
        finally
        {
            try { File.Delete(palettePath); } catch { }
        }
    }

    private static async Task RunFfmpegAsync(string ffmpegPath, string args)
    {
        var psi = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardError = true
        };

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("FFmpeg 프로세스를 시작할 수 없습니다.");
        await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"FFmpeg GIF 인코딩 실패 (exit {process.ExitCode})");
    }

    /// <summary>System.Drawing 기반 네이티브 GIF 인코딩 (FFmpeg 없을 때 fallback)</summary>
    private static void EncodeGifNative(List<string> framePaths, int fps, string outputPath)
    {
        // GIF 프레임 딜레이 (1/100초 단위)
        var delay = (int)Math.Round(100.0 / fps);

        using var firstFrame = Image.FromFile(framePaths[0]);

        // 첫 프레임 저장 (GIF 헤더 포함)
        var gifEncoder = GetGifEncoder();

        // FileStream 직접 쓰기 — MemoryStream 전체 누적 방지
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536);
        using var bw = new BinaryWriter(fs);

        // GIF89a 헤더
        bw.Write(new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }); // "GIF89a"

        var width = firstFrame.Width;
        var height = firstFrame.Height;

        // Logical Screen Descriptor
        bw.Write((ushort)width);
        bw.Write((ushort)height);
        bw.Write((byte)0xF7); // GCT flag, 256 colors
        bw.Write((byte)0);    // bg color index
        bw.Write((byte)0);    // pixel aspect ratio

        // Global Color Table (256 colors - grayscale placeholder, 실제는 프레임별)
        for (var i = 0; i < 256; i++)
        {
            bw.Write((byte)i);
            bw.Write((byte)i);
            bw.Write((byte)i);
        }

        // Netscape Application Extension (무한 루프)
        bw.Write((byte)0x21); // Extension introducer
        bw.Write((byte)0xFF); // Application extension
        bw.Write((byte)0x0B); // Block size
        bw.Write("NETSCAPE2.0"u8);
        bw.Write((byte)0x03); // Sub-block size
        bw.Write((byte)0x01); // Loop sub-block ID
        bw.Write((ushort)0);  // 무한 루프
        bw.Write((byte)0x00); // Block terminator

        firstFrame.Dispose();

        foreach (var framePath in framePaths)
        {
            using var frame = new Bitmap(framePath);
            using var quantized = QuantizeTo8Bit(frame);

            // Graphic Control Extension
            bw.Write((byte)0x21); // Extension introducer
            bw.Write((byte)0xF9); // Graphic control
            bw.Write((byte)0x04); // Block size
            bw.Write((byte)0x00); // Disposal method
            bw.Write((ushort)delay);
            bw.Write((byte)0x00); // Transparent color index
            bw.Write((byte)0x00); // Block terminator

            // 프레임을 GIF로 인코딩하고 이미지 데이터만 추출
            using var frameMs = new MemoryStream();
            quantized.Save(frameMs, gifEncoder, null);
            var frameData = frameMs.ToArray();

            // GIF 파일에서 Image Descriptor + LCT + 이미지 데이터 추출 (헤더/트레일러 제외)
            // 단순 접근: 개별 프레임 GIF에서 이미지 블록 추출
            var imgDescIdx = FindImageDescriptor(frameData);
            if (imgDescIdx >= 0)
            {
                // Image Descriptor부터 Trailer(0x3B) 직전까지 기록
                var endIdx = frameData.Length - 1; // 마지막 0x3B 제외
                bw.Write(frameData, imgDescIdx, endIdx - imgDescIdx);
            }
        }

        // GIF Trailer
        bw.Write((byte)0x3B);

        bw.Flush();
    }

    private static int FindImageDescriptor(byte[] data)
    {
        for (var i = 0; i < data.Length; i++)
        {
            if (data[i] == 0x2C) // Image Descriptor
                return i;
        }
        return -1;
    }

    private static Bitmap QuantizeTo8Bit(Bitmap source)
    {
        var result = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);

        var srcData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        // 1단계: 색상 히스토그램 수집 (5비트 축소 → 32768 버킷)
        var histogram = new int[32768];
        var colorMap = new int[32768 * 3]; // R,G,B 합계

        unsafe
        {
            for (var y = 0; y < source.Height; y++)
            {
                var row = (byte*)srcData.Scan0 + y * srcData.Stride;
                for (var x = 0; x < source.Width; x++)
                {
                    int b = row[x * 3];
                    int g = row[x * 3 + 1];
                    int r = row[x * 3 + 2];
                    var idx = ((r >> 3) << 10) | ((g >> 3) << 5) | (b >> 3);
                    histogram[idx]++;
                    colorMap[idx * 3] += r;
                    colorMap[idx * 3 + 1] += g;
                    colorMap[idx * 3 + 2] += b;
                }
            }
        }

        // 2단계: Median Cut으로 256색 팔레트 생성
        var paletteColors = MedianCut(histogram, colorMap, 256);
        var palette = result.Palette;
        for (var i = 0; i < 256; i++)
        {
            palette.Entries[i] = i < paletteColors.Length
                ? paletteColors[i]
                : Color.Black;
        }
        result.Palette = palette;

        // 3단계: 각 픽셀에 가장 가까운 팔레트 색상 매핑
        var dstData = result.LockBits(new Rectangle(0, 0, result.Width, result.Height),
            ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);

        unsafe
        {
            for (var y = 0; y < source.Height; y++)
            {
                var srcRow = (byte*)srcData.Scan0 + y * srcData.Stride;
                var dstRow = (byte*)dstData.Scan0 + y * dstData.Stride;

                for (var x = 0; x < source.Width; x++)
                {
                    int b = srcRow[x * 3];
                    int g = srcRow[x * 3 + 1];
                    int r = srcRow[x * 3 + 2];
                    dstRow[x] = (byte)FindClosestColor(paletteColors, r, g, b);
                }
            }
        }

        source.UnlockBits(srcData);
        result.UnlockBits(dstData);

        return result;
    }

    private static int FindClosestColor(Color[] palette, int r, int g, int b)
    {
        var bestIdx = 0;
        var bestDist = int.MaxValue;
        for (var i = 0; i < palette.Length; i++)
        {
            var dr = r - palette[i].R;
            var dg = g - palette[i].G;
            var db = b - palette[i].B;
            var dist = dr * dr + dg * dg + db * db;
            if (dist < bestDist)
            {
                bestDist = dist;
                bestIdx = i;
                if (dist == 0) break;
            }
        }
        return bestIdx;
    }

    private static Color[] MedianCut(int[] histogram, int[] colorMap, int maxColors)
    {
        // 사용된 색상 인덱스 수집
        var indices = new List<int>();
        for (var i = 0; i < histogram.Length; i++)
        {
            if (histogram[i] > 0) indices.Add(i);
        }

        if (indices.Count <= maxColors)
        {
            return indices.Select(idx =>
            {
                var count = histogram[idx];
                return Color.FromArgb(
                    colorMap[idx * 3] / count,
                    colorMap[idx * 3 + 1] / count,
                    colorMap[idx * 3 + 2] / count);
            }).ToArray();
        }

        var boxes = new List<(List<int> indices, int volume)> { (indices, ComputeVolume(indices)) };

        while (boxes.Count < maxColors)
        {
            // 가장 큰 box를 분할
            var maxVol = -1;
            var splitIdx = 0;
            for (var i = 0; i < boxes.Count; i++)
            {
                if (boxes[i].indices.Count > 1 && boxes[i].volume > maxVol)
                {
                    maxVol = boxes[i].volume;
                    splitIdx = i;
                }
            }

            if (maxVol <= 0) break;

            var box = boxes[splitIdx];
            boxes.RemoveAt(splitIdx);

            // 가장 넓은 채널 찾기
            int rMin = 31, rMax = 0, gMin = 31, gMax = 0, bMin = 31, bMax = 0;
            foreach (var idx in box.indices)
            {
                var r = (idx >> 10) & 31;
                var g = (idx >> 5) & 31;
                var b = idx & 31;
                if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                if (b < bMin) bMin = b; if (b > bMax) bMax = b;
            }

            var rRange = rMax - rMin;
            var gRange = gMax - gMin;
            var bRange = bMax - bMin;

            // 가장 넓은 채널 기준 정렬 후 중앙 분할
            int channel; // 0=R, 1=G, 2=B
            if (rRange >= gRange && rRange >= bRange) channel = 0;
            else if (gRange >= bRange) channel = 1;
            else channel = 2;

            box.indices.Sort((a, b2) =>
            {
                var va = channel switch { 0 => (a >> 10) & 31, 1 => (a >> 5) & 31, _ => a & 31 };
                var vb = channel switch { 0 => (b2 >> 10) & 31, 1 => (b2 >> 5) & 31, _ => b2 & 31 };
                return va.CompareTo(vb);
            });

            var mid = box.indices.Count / 2;
            var left = box.indices.GetRange(0, mid);
            var right = box.indices.GetRange(mid, box.indices.Count - mid);

            boxes.Add((left, ComputeVolume(left)));
            boxes.Add((right, ComputeVolume(right)));
        }

        return boxes.Select(box =>
        {
            long rSum = 0, gSum = 0, bSum = 0, totalCount = 0;
            foreach (var idx in box.indices)
            {
                var count = histogram[idx];
                rSum += colorMap[idx * 3] ;
                gSum += colorMap[idx * 3 + 1];
                bSum += colorMap[idx * 3 + 2];
                totalCount += count;
            }
            if (totalCount == 0) return Color.Black;
            return Color.FromArgb(
                (int)Math.Clamp(rSum / totalCount, 0, 255),
                (int)Math.Clamp(gSum / totalCount, 0, 255),
                (int)Math.Clamp(bSum / totalCount, 0, 255));
        }).ToArray();

        static int ComputeVolume(List<int> indices)
        {
            int rMin = 31, rMax = 0, gMin = 31, gMax = 0, bMin = 31, bMax = 0;
            foreach (var idx in indices)
            {
                var r = (idx >> 10) & 31;
                var g = (idx >> 5) & 31;
                var b = idx & 31;
                if (r < rMin) rMin = r; if (r > rMax) rMax = r;
                if (g < gMin) gMin = g; if (g > gMax) gMax = g;
                if (b < bMin) bMin = b; if (b > bMax) bMax = b;
            }
            return (rMax - rMin + 1) * (gMax - gMin + 1) * (bMax - bMin + 1);
        }
    }

    private static ImageCodecInfo GetGifEncoder()
    {
        return ImageCodecInfo.GetImageEncoders()
            .First(e => e.MimeType == "image/gif");
    }

    private static string? FindFfmpeg()
    {
        // 1. PATH에서 검색
        var pathDirs = Environment.GetEnvironmentVariable("PATH")?.Split(';') ?? [];
        foreach (var dir in pathDirs)
        {
            if (string.IsNullOrWhiteSpace(dir)) continue;
            var candidate = Path.Combine(dir.Trim(), "ffmpeg.exe");
            if (File.Exists(candidate)) return candidate;
        }

        // 2. 일반적인 설치 경로 확인
        var commonPaths = new List<string>
        {
            @"C:\ffmpeg\bin\ffmpeg.exe",
            @"C:\Program Files\ffmpeg\bin\ffmpeg.exe",
            @"C:\ProgramData\chocolatey\bin\ffmpeg.exe",
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                @"scoop\shims\ffmpeg.exe")
        };

        // 3. winget 패키지 경로 검색 (Gyan.FFmpeg)
        var wingetPkgs = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WinGet\Packages");

        if (Directory.Exists(wingetPkgs))
        {
            try
            {
                foreach (var dir in Directory.GetDirectories(wingetPkgs, "Gyan.FFmpeg*"))
                {
                    // ffmpeg-X.X.X-full_build/bin/ffmpeg.exe 패턴 탐색
                    foreach (var binDir in Directory.GetDirectories(dir, "bin", SearchOption.AllDirectories))
                    {
                        var candidate = Path.Combine(binDir, "ffmpeg.exe");
                        if (File.Exists(candidate)) return candidate;
                    }
                }
            }
            catch { /* 권한 문제 등 무시 */ }
        }

        // 4. winget Links 경로
        var wingetLinks = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Microsoft\WinGet\Links\ffmpeg.exe");
        commonPaths.Add(wingetLinks);

        return commonPaths.FirstOrDefault(File.Exists);
    }
}
