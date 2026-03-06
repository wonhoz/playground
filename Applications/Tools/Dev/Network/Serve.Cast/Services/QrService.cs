using System.IO;
using System.Windows.Media.Imaging;
using QRCoder;

namespace ServeCast.Services;

/// <summary>URL을 QR 코드 WPF BitmapImage로 변환</summary>
public static class QrService
{
    public static BitmapImage Generate(string url, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        var data    = generator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
        var qr      = new BitmapByteQRCode(data);

        // 다크 테마: 연한 모듈 (#CDD6F4), 어두운 배경 (#1E1E2E)
        var bytes = qr.GetGraphic(pixelsPerModule, "#CDD6F4", "#1E1E2E");

        var bitmap = new BitmapImage();
        using var ms = new MemoryStream(bytes);
        bitmap.BeginInit();
        bitmap.CacheOption  = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = ms;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
