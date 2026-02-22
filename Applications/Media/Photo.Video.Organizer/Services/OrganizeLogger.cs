using System.IO;
using System.Text;

namespace Photo.Video.Organizer.Services
{
    /// <summary>
    /// 파일 정리 결과를 CSV 로그 파일로 저장
    /// </summary>
    public static class OrganizeLogger
    {
        /// <summary>
        /// 정리 결과를 대상 폴더 루트에 CSV 파일로 저장
        /// </summary>
        public static async Task<string> SaveLogAsync(FileOrganizer.OrganizeSummary summary, string destinationRoot)
        {
            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var logPath = Path.Combine(destinationRoot, $"organizer_log_{timestamp}.csv");

            var sb = new StringBuilder();
            sb.AppendLine("원본 경로,대상 경로,미디어 날짜,미디어 유형,결과,비고");

            foreach (var result in summary.Results)
            {
                var status = result.Success ? "성공"
                    : result.IsSkippedAsDuplicate ? "중복 건너뜀"
                    : result.ErrorMessage?.Contains("건너뜀") == true ? "건너뜀"
                    : "오류";

                var mediaDate = result.MediaDate?.ToString("yyyy-MM-dd HH:mm:ss") ?? "";

                var mediaType = result.MediaType switch
                {
                    MediaType.Image => "이미지",
                    MediaType.Video => "동영상",
                    MediaType.Other => "기타",
                    _ => "알 수 없음"
                };

                var note = result.ErrorMessage ?? "";

                sb.AppendLine($"\"{result.SourcePath}\",\"{result.DestinationPath ?? ""}\",\"{mediaDate}\",\"{mediaType}\",\"{status}\",\"{note}\"");
            }

            await File.WriteAllTextAsync(logPath, sb.ToString(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            return logPath;
        }
    }
}
