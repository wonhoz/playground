namespace ScreenRecorder.Services;

/// <summary>프레임 시퀀스를 MP4/GIF로 인코딩하는 서비스 (Phase 5, 6에서 구현)</summary>
public static class EncoderService
{
    public static Task EncodeToMp4Async(List<string> framePaths, int fps, string outputPath)
    {
        // Phase 5에서 구현
        return Task.CompletedTask;
    }

    public static Task EncodeToGifAsync(List<string> framePaths, int fps, string outputPath)
    {
        // Phase 6에서 구현
        return Task.CompletedTask;
    }
}
