namespace Brush.Scale.Services;

/// <summary>
/// 각 모델의 다운로드 메타데이터.
/// URL은 HuggingFace / GitHub Releases 공개 ONNX 파일 기준.
/// 업데이트가 필요하면 DownloadUrl만 수정하면 됩니다.
/// </summary>
public record ModelInfo(
    UpscaleModelType Type,
    string           FileName,
    string           DownloadUrl,
    string           Description,
    long             ExpectedBytes   // 0 = unknown
);

public static class ModelRegistry
{
    // ── 공개 ONNX 모델 URL ────────────────────────────────────────────────
    // 출처: Hugging Face onnx-community / HuggingFace rocca / GitHub Releases
    // * waifu2x-cunet   : AaronFeng753/Waifu2x-Extension-GUI (ONNX export)
    // * RealESRGAN      : xinntao/Real-ESRGAN 공식 + community ONNX 변환
    //   → 아래 URL은 HuggingFace의 안정적인 배포 경로 사용
    static readonly IReadOnlyDictionary<UpscaleModelType, ModelInfo> _registry =
        new Dictionary<UpscaleModelType, ModelInfo>
        {
            [UpscaleModelType.Waifu2xCunet] = new(
                UpscaleModelType.Waifu2xCunet,
                "waifu2x_cunet.onnx",
                "https://huggingface.co/Xenova/waifu2x/resolve/main/waifu2x_cunet_scale2_nf_art_noise1.onnx",
                "waifu2x-cunet (일러스트/애니 특화, 2x)",
                0
            ),
            [UpscaleModelType.RealESRGANPhoto] = new(
                UpscaleModelType.RealESRGANPhoto,
                "realesrgan_x4plus.onnx",
                "https://huggingface.co/rocca/upscaler-onnx/resolve/main/realesrgan_x4plus.onnx",
                "RealESRGAN x4plus (실사 사진 4x)",
                0
            ),
            [UpscaleModelType.RealESRGANAnime] = new(
                UpscaleModelType.RealESRGANAnime,
                "realesrgan_x4plus_anime.onnx",
                "https://huggingface.co/rocca/upscaler-onnx/resolve/main/realesrgan_x4plus_anime_6B.onnx",
                "RealESRGAN Anime 6B (애니메이션 4x)",
                0
            ),
        };

    public static ModelInfo? Get(UpscaleModelType t)
        => _registry.GetValueOrDefault(t);

    public static IReadOnlyCollection<ModelInfo> All
        => _registry.Values.ToList();
}
