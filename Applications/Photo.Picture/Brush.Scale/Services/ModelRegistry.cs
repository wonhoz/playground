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
    // * waifu2x-cunet   : deepghs/waifu2x_onnx (nagadomi/nunif 공식 릴리즈 기반)
    // * RealESRGAN x4+  : imgdesignart/realesrgan-x4-onnx
    // * RealESRGAN Anime: 공개 ONNX 변환본 없음 → 수동 설치 필요
    //   파일명: realesrgan_x4plus_anime.onnx → 모델 폴더에 직접 배치
    static readonly IReadOnlyDictionary<UpscaleModelType, ModelInfo> _registry =
        new Dictionary<UpscaleModelType, ModelInfo>
        {
            [UpscaleModelType.Waifu2xCunet] = new(
                UpscaleModelType.Waifu2xCunet,
                "waifu2x_cunet.onnx",
                "https://huggingface.co/deepghs/waifu2x_onnx/resolve/main/20250502/onnx_models/cunet/art/noise1_scale2x.onnx",
                "waifu2x-cunet (일러스트/애니 특화, 2x) — 5 MB",
                5_426_000
            ),
            [UpscaleModelType.RealESRGANPhoto] = new(
                UpscaleModelType.RealESRGANPhoto,
                "realesrgan_x4plus.onnx",
                "https://huggingface.co/imgdesignart/realesrgan-x4-onnx/resolve/main/onnx/model.onnx",
                "RealESRGAN x4plus (실사 사진 4x) — 67 MB",
                70_254_000
            ),
            [UpscaleModelType.RealESRGANAnime] = new(
                UpscaleModelType.RealESRGANAnime,
                "realesrgan_x4plus_anime.onnx",
                "",   // 공개 ONNX 변환본 없음 — 수동 설치 필요
                "RealESRGAN Anime 6B (애니메이션 4x)",
                0
            ),
        };

    public static ModelInfo? Get(UpscaleModelType t)
        => _registry.GetValueOrDefault(t);

    public static IReadOnlyCollection<ModelInfo> All
        => _registry.Values.ToList();
}
