using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.QuickTime;
using System.Globalization;
using System.IO;

namespace Photo.Video.Organizer.Services
{
    /// <summary>
    /// 미디어 파일(사진/동영상)에서 촬영 날짜를 추출하는 서비스
    /// </summary>
    public static class MediaDateExtractor
    {
        // 지원하는 이미지 확장자
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".heic", ".heif", ".tiff", ".tif", ".cr2", ".nef", ".arw", ".dng", ".raf", ".orf"
        };

        // 지원하는 비디오 확장자
        private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".mp4", ".mov", ".m4v", ".3gp", ".avi", ".mkv", ".wmv", ".mpg", ".mpeg"
        };

        // 기타 지원 확장자 (메타데이터 없음, 수정일 사용)
        private static readonly HashSet<string> OtherExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".gif", ".bmp", ".webp", ".ico", ".svg"
        };

        /// <summary>
        /// 파일이 지원되는 미디어 파일인지 확인
        /// </summary>
        public static bool IsSupportedMediaFile(string filePath)
        {
            var ext = Path.GetExtension(filePath);
            return ImageExtensions.Contains(ext) || VideoExtensions.Contains(ext) || OtherExtensions.Contains(ext);
        }

        /// <summary>
        /// 미디어 파일에서 촬영 날짜 추출
        /// 우선순위: EXIF/QuickTime 메타데이터 > 파일 수정 날짜
        /// </summary>
        public static DateTime GetMediaDate(string filePath)
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();

            // 메타데이터가 없는 파일 형식은 바로 수정일 사용
            if (OtherExtensions.Contains(extension))
            {
                return GetFileModifiedDate(filePath);
            }

            try
            {
                var directories = ImageMetadataReader.ReadMetadata(filePath);

                // 1. EXIF 원본 촬영 날짜 (사진)
                var exifSubIfd = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();
                if (exifSubIfd != null)
                {
                    if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out var originalDate))
                        return originalDate;

                    if (exifSubIfd.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out var digitizedDate))
                        return digitizedDate;
                }

                // 2. EXIF IFD0 날짜
                var exifIfd0 = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
                if (exifIfd0 != null)
                {
                    if (exifIfd0.TryGetDateTime(ExifDirectoryBase.TagDateTime, out var dateTime))
                        return dateTime;
                }

                // 3. QuickTime 생성 날짜 (동영상)
                var quickTime = directories.OfType<QuickTimeMovieHeaderDirectory>().FirstOrDefault();
                if (quickTime != null)
                {
                    if (quickTime.TryGetDateTime(QuickTimeMovieHeaderDirectory.TagCreated, out var createdDate))
                    {
                        // QuickTime은 UTC로 저장됨, 로컬 시간으로 변환
                        if (createdDate.Year > 1970) // 유효한 날짜인지 확인
                            return createdDate.ToLocalTime();
                    }
                }

                // 4. QuickTime 메타데이터 디렉토리
                var quickTimeMeta = directories.OfType<QuickTimeMetadataHeaderDirectory>().FirstOrDefault();
                if (quickTimeMeta != null)
                {
                    var creationDateTag = quickTimeMeta.Tags
                        .FirstOrDefault(t => t.Name?.Contains("Creation Date", StringComparison.OrdinalIgnoreCase) == true);

                    if (creationDateTag != null && DateTime.TryParse(creationDateTag.Description, out var metaDate))
                        return metaDate;
                }

                // 5. 파일명에서 날짜 추출 시도 (IMG_20231225_123456.jpg, VID_20231225_123456.mp4 등)
                var fileNameDate = TryExtractDateFromFileName(filePath);
                if (fileNameDate.HasValue)
                    return fileNameDate.Value;
            }
            catch
            {
                // 메타데이터 읽기 실패 시 무시
            }

            // 6. 최후의 수단: 파일 수정 날짜
            return GetFileModifiedDate(filePath);
        }

        /// <summary>
        /// 파일명에서 날짜 추출 시도
        /// 지원 패턴: IMG_20231225_123456, VID_20231225_123456, 20231225_123456, 2023-12-25 12.34.56 등
        /// </summary>
        private static DateTime? TryExtractDateFromFileName(string filePath)
        {
            var fileName = Path.GetFileNameWithoutExtension(filePath);

            // 패턴 1: IMG_20231225_123456 또는 VID_20231225_123456
            var patterns = new[]
            {
                @"(\d{4})(\d{2})(\d{2})_(\d{2})(\d{2})(\d{2})",  // 20231225_123456
                @"(\d{4})-(\d{2})-(\d{2})[\s_](\d{2})\.(\d{2})\.(\d{2})", // 2023-12-25 12.34.56
                @"(\d{4})(\d{2})(\d{2})",  // 20231225 (시간 없음)
            };

            foreach (var pattern in patterns)
            {
                var match = System.Text.RegularExpressions.Regex.Match(fileName, pattern);
                if (match.Success)
                {
                    try
                    {
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        int hour = match.Groups.Count > 4 && match.Groups[4].Success ? int.Parse(match.Groups[4].Value) : 0;
                        int minute = match.Groups.Count > 5 && match.Groups[5].Success ? int.Parse(match.Groups[5].Value) : 0;
                        int second = match.Groups.Count > 6 && match.Groups[6].Success ? int.Parse(match.Groups[6].Value) : 0;

                        if (year >= 1990 && year <= 2100 && month >= 1 && month <= 12
                            && day >= 1 && day <= DateTime.DaysInMonth(year, month))
                        {
                            return new DateTime(year, month, day, hour, minute, second);
                        }
                    }
                    catch
                    {
                        // 파싱 실패 시 무시
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// 파일 수정 날짜 반환
        /// </summary>
        private static DateTime GetFileModifiedDate(string filePath)
        {
            var fileInfo = new FileInfo(filePath);
            return fileInfo.LastWriteTime;
        }

        /// <summary>
        /// 파일 타입 반환
        /// </summary>
        public static MediaType GetMediaType(string filePath)
        {
            var ext = Path.GetExtension(filePath);

            if (ImageExtensions.Contains(ext))
                return MediaType.Image;

            if (VideoExtensions.Contains(ext))
                return MediaType.Video;

            if (OtherExtensions.Contains(ext))
                return MediaType.Other;

            return MediaType.Unknown;
        }
    }

    public enum MediaType
    {
        Unknown,
        Image,
        Video,
        Other
    }
}
