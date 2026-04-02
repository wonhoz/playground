namespace Brush.Scale.Models;

public record UpscaleJob(
    string     InputPath,
    string     OutputPath,
    UpscaleModelType Model,
    int        ScaleFactor,
    OutputFormat Format,
    int        JpegQuality
);

public enum OutputFormat { Png, Jpg, WebP }

public static class OutputFormatExtensions
{
    public static string Extension(this OutputFormat f) => f switch
    {
        OutputFormat.Jpg  => ".jpg",
        OutputFormat.WebP => ".webp",
        _                 => ".png",
    };

    public static string Filter(this OutputFormat f) => f switch
    {
        OutputFormat.Jpg  => "JPEG|*.jpg",
        OutputFormat.WebP => "WebP|*.webp",
        _                 => "PNG|*.png",
    };
}
