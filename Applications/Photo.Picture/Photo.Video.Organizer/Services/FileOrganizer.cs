using System.IO;

namespace Photo.Video.Organizer.Services
{
    /// <summary>
    /// 폴더 구조 옵션
    /// </summary>
    public enum FolderStructure
    {
        /// <summary>
        /// yyyy/MM/파일명 (기본)
        /// </summary>
        YearMonth,

        /// <summary>
        /// yyyy/MM/yyyy-MM-dd/파일명
        /// </summary>
        YearMonthDay,

        /// <summary>
        /// 사용자 정의 패턴 (/ 구분자로 폴더 계층 구조)
        /// 예: "yyyy/MM/dd" → 2024/01/15/파일명
        /// </summary>
        Custom
    }

    /// <summary>
    /// 미디어 파일을 년/월 폴더 구조로 정리하는 서비스
    /// </summary>
    public class FileOrganizer
    {
        /// <summary>
        /// 파일 처리 결과
        /// </summary>
        public class OrganizeResult
        {
            public string SourcePath { get; set; } = "";
            public string? DestinationPath { get; set; }
            public bool Success { get; set; }
            public bool IsSkippedAsDuplicate { get; set; }
            public bool AutoRotated { get; set; }
            public bool Moved { get; set; }
            public string? ErrorMessage { get; set; }
            public DateTime? MediaDate { get; set; }
            public MediaType MediaType { get; set; }
        }

        /// <summary>
        /// 전체 정리 결과
        /// </summary>
        public class OrganizeSummary
        {
            public int TotalFiles { get; set; }
            public int SuccessCount { get; set; }
            public int ImageCount { get; set; }
            public int VideoCount { get; set; }
            public int SkippedCount { get; set; }
            public int DuplicateCount { get; set; }
            public int ErrorCount { get; set; }
            public int RotatedCount { get; set; }
            public List<OrganizeResult> Results { get; set; } = new();
            public string? LogFilePath { get; set; }
        }

        /// <summary>
        /// 미리보기 결과 (실제 파일 처리 없이 대상 경로만 계산)
        /// </summary>
        public class PreviewEntry
        {
            public string SourceFileName { get; set; } = "";
            public string DestinationFolder { get; set; } = "";
            public DateTime MediaDate { get; set; }
        }

        /// <summary>
        /// 커스텀 패턴이 유효한지 검사. 오류 메시지 반환 (null이면 유효)
        /// </summary>
        public static string? ValidateCustomPattern(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern))
                return "패턴을 입력하세요.";

            var parts = pattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return "올바른 패턴을 입력하세요.";

            var testDate = new DateTime(2024, 6, 15, 14, 30, 0);
            try
            {
                foreach (var part in parts)
                    testDate.ToString(part);
            }
            catch
            {
                return $"잘못된 날짜 형식 패턴입니다.";
            }

            return null;
        }

        /// <summary>
        /// 미리보기: 실제 복사 없이 각 파일의 대상 폴더를 계산하여 반환
        /// </summary>
        public async Task<List<PreviewEntry>> PreviewFilesAsync(
            IEnumerable<string> files,
            string destinationRoot,
            FolderStructure folderStructure = FolderStructure.YearMonth,
            string? customPattern = null,
            CancellationToken cancellationToken = default)
        {
            var fileList = files.ToList();
            return await Task.Run(() =>
            {
                var entries = new List<PreviewEntry>();
                foreach (var filePath in fileList)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        if (!MediaDateExtractor.IsSupportedMediaFile(filePath))
                            continue;

                        var mediaDate = MediaDateExtractor.GetMediaDate(filePath);
                        var folder = BuildDestinationFolder(destinationRoot, mediaDate, folderStructure, customPattern);
                        entries.Add(new PreviewEntry
                        {
                            SourceFileName = Path.GetFileName(filePath),
                            DestinationFolder = folder,
                            MediaDate = mediaDate
                        });
                    }
                    catch { }
                }
                return entries;
            }, cancellationToken);
        }

        /// <summary>
        /// 파일들을 지정된 경로에 폴더 구조로 정리
        /// </summary>
        public async Task<OrganizeSummary> OrganizeFilesAsync(
            IEnumerable<string> files,
            string destinationRoot,
            FolderStructure folderStructure = FolderStructure.YearMonth,
            string? customPattern = null,
            bool autoRotate = false,
            bool moveFiles = false,
            IProgress<(int current, int total, string fileName)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var fileList = files.ToList();
            var summary = new OrganizeSummary { TotalFiles = fileList.Count };

            // 단일 Task.Run으로 전체 루프 처리 (파일별 Task 생성 오버헤드 제거)
            var results = await Task.Run(() =>
            {
                var list = new List<OrganizeResult>();
                for (int i = 0; i < fileList.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var filePath = fileList[i];
                    progress?.Report((i + 1, fileList.Count, Path.GetFileName(filePath)));
                    list.Add(OrganizeSingleFile(filePath, destinationRoot, folderStructure, customPattern, autoRotate, moveFiles));
                }
                return list;
            }, cancellationToken);

            foreach (var result in results)
            {
                summary.Results.Add(result);
                if (result.Success)
                {
                    summary.SuccessCount++;
                    if (result.MediaType == MediaType.Image) summary.ImageCount++;
                    else if (result.MediaType == MediaType.Video) summary.VideoCount++;
                    if (result.AutoRotated) summary.RotatedCount++;
                }
                else if (result.IsSkippedAsDuplicate)
                    summary.DuplicateCount++;
                else if (result.ErrorMessage?.Contains("건너뜀") == true)
                    summary.SkippedCount++;
                else
                    summary.ErrorCount++;
            }

            return summary;
        }

        /// <summary>
        /// 단일 파일 정리
        /// </summary>
        private OrganizeResult OrganizeSingleFile(
            string sourcePath, string destinationRoot,
            FolderStructure folderStructure, string? customPattern,
            bool autoRotate, bool moveFiles)
        {
            var result = new OrganizeResult { SourcePath = sourcePath };

            try
            {
                if (!MediaDateExtractor.IsSupportedMediaFile(sourcePath))
                {
                    result.ErrorMessage = "지원하지 않는 파일 형식 (건너뜀)";
                    return result;
                }

                var mediaDate = MediaDateExtractor.GetMediaDate(sourcePath);
                result.MediaDate = mediaDate;
                result.MediaType = MediaDateExtractor.GetMediaType(sourcePath);

                var destinationFolder = BuildDestinationFolder(destinationRoot, mediaDate, folderStructure, customPattern);
                Directory.CreateDirectory(destinationFolder);

                // SHA256 해시 기반 중복 파일 감지 (크기가 같을 때만 해시 계산)
                long sourceSize = new FileInfo(sourcePath).Length;
                string? sourceHash = null;
                foreach (var existingFile in Directory.EnumerateFiles(destinationFolder))
                {
                    if (new FileInfo(existingFile).Length != sourceSize) continue;
                    sourceHash ??= ComputeFileHash(sourcePath);
                    if (ComputeFileHash(existingFile) == sourceHash)
                    {
                        result.IsSkippedAsDuplicate = true;
                        result.ErrorMessage = $"동일한 파일이 이미 존재합니다 (건너뜀): {Path.GetFileName(existingFile)}";
                        return result;
                    }
                }

                // 새 파일명 생성 (yyyy-MM-dd HH.mm.ss.확장자)
                var extension = Path.GetExtension(sourcePath);
                var newFileName = $"{mediaDate:yyyy-MM-dd HH.mm.ss}{extension}";
                var destinationPath = GetUniqueFilePath(Path.Combine(destinationFolder, newFileName));

                if (moveFiles)
                {
                    // 이동: 같은 드라이브면 Move, 다른 드라이브면 Copy→Delete
                    if (autoRotate && result.MediaType == MediaType.Image)
                    {
                        result.AutoRotated = ExifRotationHelper.RotateAndSave(sourcePath, destinationPath);
                        if (result.AutoRotated || File.Exists(destinationPath))
                            File.Delete(sourcePath);
                    }
                    else
                    {
                        File.Move(sourcePath, destinationPath);
                    }
                    result.Moved = true;
                }
                else
                {
                    // 복사
                    if (autoRotate && result.MediaType == MediaType.Image)
                        result.AutoRotated = ExifRotationHelper.RotateAndSave(sourcePath, destinationPath);
                    else
                        File.Copy(sourcePath, destinationPath, overwrite: false);
                }

                result.DestinationPath = destinationPath;
                result.Success = true;
            }
            catch (Exception ex)
            {
                result.ErrorMessage = ex.Message;
            }

            return result;
        }

        /// <summary>
        /// 폴더 구조 옵션에 따라 대상 폴더 경로 계산
        /// </summary>
        private static string BuildDestinationFolder(
            string destinationRoot, DateTime mediaDate,
            FolderStructure folderStructure, string? customPattern)
        {
            if (folderStructure == FolderStructure.Custom && !string.IsNullOrWhiteSpace(customPattern))
            {
                var parts = customPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
                var folderParts = new[] { destinationRoot }.Concat(parts.Select(p => mediaDate.ToString(p))).ToArray();
                return Path.Combine(folderParts);
            }
            if (folderStructure == FolderStructure.YearMonthDay)
            {
                return Path.Combine(destinationRoot,
                    mediaDate.Year.ToString("D4"),
                    mediaDate.Month.ToString("D2"),
                    $"{mediaDate:yyyy-MM-dd}");
            }
            return Path.Combine(destinationRoot,
                mediaDate.Year.ToString("D4"),
                mediaDate.Month.ToString("D2"));
        }

        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        private static string GetUniqueFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return filePath;

            var directory = Path.GetDirectoryName(filePath) ?? "";
            var fileNameWithoutExt = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            int counter = 1;
            string newPath;
            do
            {
                newPath = Path.Combine(directory, $"{fileNameWithoutExt}_{counter}{extension}");
                counter++;
            } while (File.Exists(newPath));
            return newPath;
        }
    }
}
