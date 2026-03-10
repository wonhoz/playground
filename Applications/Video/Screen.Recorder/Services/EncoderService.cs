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
    public static async Task EncodeToMp4Async(List<string> framePaths, int fps, string outputPath)
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

        // 프레임 파일을 연번 심볼릭 링크로 정리 (FFmpeg 입력용)
        var tempDir = Path.Combine(Path.GetTempPath(), $"sr_enc_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            // 프레임 파일 복사 (연번)
            for (var i = 0; i < framePaths.Count; i++)
            {
                var dest = Path.Combine(tempDir, $"frame_{i:D6}.png");
                File.Copy(framePaths[i], dest);
            }

            var inputPattern = Path.Combine(tempDir, "frame_%06d.png");

            // FFmpeg 인코딩: H.264, yuv420p (호환성), CRF 23 (적정 품질)
            var args = $"-framerate {fps} -i \"{inputPattern}\" " +
                       $"-c:v libx264 -pix_fmt yuv420p -crf 23 -preset fast " +
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
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
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
        var tempDir = Path.Combine(Path.GetTempPath(), $"sr_gif_{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);

        try
        {
            for (var i = 0; i < framePaths.Count; i++)
            {
                var dest = Path.Combine(tempDir, $"frame_{i:D6}.png");
                File.Copy(framePaths[i], dest);
            }

            var inputPattern = Path.Combine(tempDir, "frame_%06d.png");
            var palettePath = Path.Combine(tempDir, "palette.png");

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
            try { Directory.Delete(tempDir, true); } catch { }
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
        var dimension = new FrameDimension(firstFrame.FrameDimensionsList[0]);

        // GIF 인코더 파라미터 설정
        var encoderParams = new EncoderParameters(1);

        // 첫 프레임 저장 (GIF 헤더 포함)
        var gifEncoder = GetGifEncoder();

        // 멀티프레임 GIF 저장을 위한 바이너리 수동 조립
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);

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
        File.WriteAllBytes(outputPath, ms.ToArray());
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

        // 간단한 양자화: 고정 팔레트 사용
        var palette = result.Palette;
        for (var i = 0; i < 256; i++)
        {
            var r = (i >> 5) * 36;
            var g = ((i >> 2) & 0x07) * 36;
            var b = (i & 0x03) * 85;
            palette.Entries[i] = Color.FromArgb(r, g, b);
        }
        result.Palette = palette;

        var srcData = source.LockBits(new Rectangle(0, 0, source.Width, source.Height),
            ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);
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
                    var b = srcRow[x * 3] / 85;       // 0-3
                    var g = srcRow[x * 3 + 1] / 36;   // 0-7
                    var r = srcRow[x * 3 + 2] / 36;   // 0-7
                    dstRow[x] = (byte)((r << 5) | (g << 2) | b);
                }
            }
        }

        source.UnlockBits(srcData);
        result.UnlockBits(dstData);

        return result;
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
