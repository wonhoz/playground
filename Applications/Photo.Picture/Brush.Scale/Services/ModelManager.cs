using System.Diagnostics;

namespace Brush.Scale.Services;

public class ModelManager
{
    static readonly string _modelDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Playground", "Brush.Scale", "models");

    public static string ModelDir => _modelDir;

    public static bool IsAvailable(UpscaleModelType t)
    {
        if (t == UpscaleModelType.Bicubic) return true;
        var path = GetModelPath(t);
        return path is not null && File.Exists(path);
    }

    public static string? GetModelPath(UpscaleModelType t)
    {
        if (t == UpscaleModelType.Bicubic) return null;
        var file = t.ModelFileName();
        if (string.IsNullOrEmpty(file)) return null;
        return Path.Combine(_modelDir, file);
    }

    public static void EnsureModelDir() => Directory.CreateDirectory(_modelDir);

    public static void OpenModelDir()
    {
        EnsureModelDir();
        Process.Start("explorer.exe", _modelDir);
    }

    public static IReadOnlyList<(UpscaleModelType Type, bool Available)> GetModelStatus()
    {
        return new[]
        {
            (UpscaleModelType.Bicubic,         true),
            (UpscaleModelType.Waifu2xCunet,    IsAvailable(UpscaleModelType.Waifu2xCunet)),
            (UpscaleModelType.RealESRGANPhoto,  IsAvailable(UpscaleModelType.RealESRGANPhoto)),
            (UpscaleModelType.RealESRGANAnime,  IsAvailable(UpscaleModelType.RealESRGANAnime)),
        };
    }
}
