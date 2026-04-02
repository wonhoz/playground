namespace Brush.Scale.Models;

public enum UpscaleModelType
{
    Bicubic,
    Waifu2xCunet,
    RealESRGANPhoto,
    RealESRGANAnime,
}

public static class UpscaleModelTypeExtensions
{
    public static string DisplayName(this UpscaleModelType t) => t switch
    {
        UpscaleModelType.Bicubic         => "Bicubic (내장 — 항상 사용 가능)",
        UpscaleModelType.Waifu2xCunet    => "waifu2x-cunet (일러스트/애니)",
        UpscaleModelType.RealESRGANPhoto => "RealESRGAN x4plus (실사 사진)",
        UpscaleModelType.RealESRGANAnime => "RealESRGAN Anime (애니메이션)",
        _                                => t.ToString(),
    };

    public static string ModelFileName(this UpscaleModelType t) => t switch
    {
        UpscaleModelType.Waifu2xCunet    => "waifu2x_cunet.onnx",
        UpscaleModelType.RealESRGANPhoto => "realesrgan_x4plus.onnx",
        UpscaleModelType.RealESRGANAnime => "realesrgan_x4plus_anime.onnx",
        _                                => string.Empty,
    };

    public static int NativeScale(this UpscaleModelType t) => t switch
    {
        UpscaleModelType.RealESRGANPhoto => 4,
        UpscaleModelType.RealESRGANAnime => 4,
        UpscaleModelType.Waifu2xCunet    => 2,
        _                                => 1,
    };
}
