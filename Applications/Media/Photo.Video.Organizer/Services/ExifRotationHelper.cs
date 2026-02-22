using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace Photo.Video.Organizer.Services
{
    /// <summary>
    /// EXIF Orientation 태그를 읽어 이미지를 회전/저장하는 헬퍼.
    /// JPEG: Quality=100 재인코딩 (실질적 무손실). PNG/BMP/TIFF: 원본 포맷 무손실 저장.
    /// EXIF 메타데이터 전체 보존, Orientation 태그를 1(정상)으로 리셋.
    /// </summary>
    public static class ExifRotationHelper
    {
        private const int OrientationTagId = 0x0112;   // EXIF Orientation

        private static readonly HashSet<string> JpegExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg" };

        private static readonly HashSet<string> TiffExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".tiff", ".tif" };

        // System.Drawing으로 처리 가능한 포맷 (RAW 계열은 지원하지 않음)
        private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
            { ".jpg", ".jpeg", ".png", ".bmp", ".tiff", ".tif" };

        /// <summary>
        /// EXIF Orientation 값을 읽어 반환합니다. 없거나 읽기 실패 시 0 반환.
        /// </summary>
        public static int GetOrientation(string filePath)
        {
            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);
                var ifd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (ifd0 != null && ifd0.TryGetInt32(ExifDirectoryBase.TagOrientation, out var ori))
                    return ori;
            }
            catch { }
            return 0;
        }

        /// <summary>
        /// 파일이 지원 포맷이고 Orientation이 정상(1)이 아닐 때 true 반환.
        /// </summary>
        public static bool NeedsRotation(string filePath)
        {
            if (!SupportedExtensions.Contains(Path.GetExtension(filePath)))
                return false;
            return GetOrientation(filePath) > 1;
        }

        /// <summary>
        /// EXIF Orientation에 따라 이미지를 회전하여 destinationPath에 저장합니다.
        /// 지원하지 않는 포맷이거나 회전 불필요 시 단순 File.Copy 수행.
        /// </summary>
        /// <returns>실제로 회전이 적용되었으면 true</returns>
        public static bool RotateAndSave(string sourcePath, string destinationPath)
        {
            var ext = Path.GetExtension(sourcePath);
            if (!SupportedExtensions.Contains(ext))
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
                return false;
            }

            var orientation = GetOrientation(sourcePath);
            if (orientation <= 1)
            {
                File.Copy(sourcePath, destinationPath, overwrite: false);
                return false;
            }

            var flipType = ToRotateFlipType(orientation);
            using var bmp = new Bitmap(sourcePath);
            bmp.RotateFlip(flipType);
            ResetOrientationTag(bmp);   // Orientation → 1(정상)로 리셋

            if (JpegExtensions.Contains(ext))
                SaveAsJpeg(bmp, destinationPath);
            else if (TiffExtensions.Contains(ext))
                bmp.Save(destinationPath, ImageFormat.Tiff);
            else if (ext.Equals(".png", StringComparison.OrdinalIgnoreCase))
                bmp.Save(destinationPath, ImageFormat.Png);
            else
                bmp.Save(destinationPath, ImageFormat.Bmp);

            return true;
        }

        // JPEG Quality=100으로 저장 (실질적 무손실, EXIF PropertyItem 자동 보존)
        private static void SaveAsJpeg(Bitmap bmp, string path)
        {
            var encoder = ImageCodecInfo.GetImageEncoders()
                .First(c => c.MimeType == "image/jpeg");
            using var ep = new EncoderParameters(1);
            ep.Param[0] = new EncoderParameter(
                System.Drawing.Imaging.Encoder.Quality, 100L);
            bmp.Save(path, encoder, ep);
        }

        // Orientation PropertyItem을 1(정상)로 리셋 — 뷰어가 이중 회전하지 않도록
        private static void ResetOrientationTag(Bitmap bmp)
        {
            try
            {
                var prop = bmp.PropertyItems.FirstOrDefault(p => p.Id == OrientationTagId);
                if (prop != null)
                {
                    prop.Value = BitConverter.GetBytes((ushort)1);
                    bmp.SetPropertyItem(prop);
                }
            }
            catch { }
        }

        // EXIF Orientation → System.Drawing.RotateFlipType 변환
        private static RotateFlipType ToRotateFlipType(int orientation) => orientation switch
        {
            2 => RotateFlipType.RotateNoneFlipX,
            3 => RotateFlipType.Rotate180FlipNone,
            4 => RotateFlipType.RotateNoneFlipY,
            5 => RotateFlipType.Rotate90FlipX,
            6 => RotateFlipType.Rotate90FlipNone,
            7 => RotateFlipType.Rotate270FlipX,
            8 => RotateFlipType.Rotate270FlipNone,
            _ => RotateFlipType.RotateNoneFlipNone
        };
    }
}
