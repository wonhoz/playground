using System.IO;
using System.Windows.Media.Imaging;

namespace Geo.Quiz.Services;

/// <summary>
/// 국기 이미지 캐시 — 메모리 + 로컬 파일(AppData).
/// 네트워크 재다운로드 없이 오프라인/재퀴즈 시 재사용.
/// </summary>
public static class FlagCache
{
    static readonly Dictionary<string, BitmapImage> _mem = [];
    static readonly string _dir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "GeoQuiz", "FlagCache");

    static FlagCache()
    {
        Directory.CreateDirectory(_dir);
    }

    /// <summary>메모리 → 파일 순으로 캐시 조회. 없으면 null.</summary>
    public static BitmapImage? GetCached(string isoCode)
    {
        if (_mem.TryGetValue(isoCode, out var bmp)) return bmp;

        var path = FilePath(isoCode);
        if (!File.Exists(path)) return null;

        try
        {
            var img = Load(path);
            _mem[isoCode] = img;
            return img;
        }
        catch { return null; }
    }

    /// <summary>다운로드 완료된 BitmapImage를 메모리 + 파일에 저장.</summary>
    public static void Add(string isoCode, BitmapImage bmp)
    {
        _mem[isoCode] = bmp;
        var path = FilePath(isoCode);
        if (File.Exists(path)) return;

        try
        {
            var enc = new PngBitmapEncoder();
            enc.Frames.Add(BitmapFrame.Create(bmp));
            using var fs = File.OpenWrite(path);
            enc.Save(fs);
        }
        catch { }
    }

    static BitmapImage Load(string path)
    {
        var img = new BitmapImage();
        img.BeginInit();
        img.CacheOption = BitmapCacheOption.OnLoad;
        using var fs = File.OpenRead(path);
        img.StreamSource = fs;
        img.EndInit();
        img.Freeze();
        return img;
    }

    static string FilePath(string isoCode) =>
        Path.Combine(_dir, $"{isoCode.ToLower()}.png");
}
