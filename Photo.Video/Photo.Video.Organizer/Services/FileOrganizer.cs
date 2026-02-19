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
            public List<OrganizeResult> Results { get; set; } = new();
            public string? LogFilePath { get; set; }
        }

        /// <summary>
        /// 파일들을 지정된 경로에 폴더 구조로 정리
        /// </summary>
        /// <param name="files">정리할 파일 경로 목록</param>
        /// <param name="destinationRoot">대상 루트 폴더</param>
        /// <param name="folderStructure">폴더 구조 옵션</param>
        /// <param name="progress">진행 상황 콜백</param>
        /// <param name="cancellationToken">취소 토큰</param>
        public async Task<OrganizeSummary> OrganizeFilesAsync(
            IEnumerable<string> files,
            string destinationRoot,
            FolderStructure folderStructure = FolderStructure.YearMonth,
            string? customPattern = null,
            IProgress<(int current, int total, string fileName)>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var fileList = files.ToList();
            var summary = new OrganizeSummary { TotalFiles = fileList.Count };

            for (int i = 0; i < fileList.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var filePath = fileList[i];
                var fileName = Path.GetFileName(filePath);

                progress?.Report((i + 1, fileList.Count, fileName));

                var result = await Task.Run(() => OrganizeSingleFile(filePath, destinationRoot, folderStructure, customPattern), cancellationToken);
                summary.Results.Add(result);

                if (result.Success)
                {
                    summary.SuccessCount++;
                    if (result.MediaType == MediaType.Image)
                        summary.ImageCount++;
                    else if (result.MediaType == MediaType.Video)
                        summary.VideoCount++;
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
        private OrganizeResult OrganizeSingleFile(string sourcePath, string destinationRoot, FolderStructure folderStructure, string? customPattern = null)
        {
            var result = new OrganizeResult { SourcePath = sourcePath };

            try
            {
                // 지원하는 파일인지 확인
                if (!MediaDateExtractor.IsSupportedMediaFile(sourcePath))
                {
                    result.ErrorMessage = "지원하지 않는 파일 형식 (건너뜀)";
                    return result;
                }

                // 미디어 날짜 추출
                var mediaDate = MediaDateExtractor.GetMediaDate(sourcePath);
                result.MediaDate = mediaDate;
                result.MediaType = MediaDateExtractor.GetMediaType(sourcePath);

                // 대상 폴더 결정
                string destinationFolder;

                if (folderStructure == FolderStructure.Custom && !string.IsNullOrWhiteSpace(customPattern))
                {
                    // 사용자 정의 패턴: "yyyy/MM/dd" → 각 부분을 날짜 포맷 적용 후 폴더 계층 생성
                    var parts = customPattern.Split('/', StringSplitOptions.RemoveEmptyEntries);
                    var folderParts = new[] { destinationRoot }.Concat(parts.Select(p => mediaDate.ToString(p))).ToArray();
                    destinationFolder = Path.Combine(folderParts);
                }
                else if (folderStructure == FolderStructure.YearMonthDay)
                {
                    // yyyy/MM/yyyy-MM-dd/
                    destinationFolder = Path.Combine(destinationRoot,
                        mediaDate.Year.ToString("D4"),
                        mediaDate.Month.ToString("D2"),
                        $"{mediaDate:yyyy-MM-dd}");
                }
                else
                {
                    // yyyy/MM/ (기본)
                    destinationFolder = Path.Combine(destinationRoot,
                        mediaDate.Year.ToString("D4"),
                        mediaDate.Month.ToString("D2"));
                }

                Directory.CreateDirectory(destinationFolder);

                // SHA256 해시 기반 중복 파일 감지 (이미 동일 내용의 파일이 대상 폴더에 있으면 건너뜀)
                if (Directory.Exists(destinationFolder))
                {
                    var sourceHash = ComputeFileHash(sourcePath);
                    foreach (var existingFile in Directory.GetFiles(destinationFolder))
                    {
                        if (ComputeFileHash(existingFile) == sourceHash)
                        {
                            result.IsSkippedAsDuplicate = true;
                            result.ErrorMessage = $"동일한 파일이 이미 존재합니다 (건너뜀): {Path.GetFileName(existingFile)}";
                            return result;
                        }
                    }
                }

                // 새 파일명 생성 (yyyy-MM-dd HH.mm.ss.확장자)
                var extension = Path.GetExtension(sourcePath);
                var newFileName = $"{mediaDate:yyyy-MM-dd HH.mm.ss}{extension}";
                var destinationPath = Path.Combine(destinationFolder, newFileName);

                // 중복 파일명 처리
                destinationPath = GetUniqueFilePath(destinationPath);

                // 파일 복사
                File.Copy(sourcePath, destinationPath, overwrite: false);

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
        /// SHA256 해시값 계산
        /// </summary>
        private static string ComputeFileHash(string filePath)
        {
            using var sha256 = System.Security.Cryptography.SHA256.Create();
            using var stream = File.OpenRead(filePath);
            return Convert.ToHexString(sha256.ComputeHash(stream));
        }

        /// <summary>
        /// 중복 파일명이 있을 경우 고유한 파일 경로 반환
        /// </summary>
        private string GetUniqueFilePath(string filePath)
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
