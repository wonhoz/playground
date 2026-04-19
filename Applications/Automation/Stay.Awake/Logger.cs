namespace StayAwake
{
    /// <summary>
    /// 간이 로그 유틸 — 주요 catch 블록에서 호출하여 무음 예외 디버깅 지원
    /// 파일 쓰기 자체가 실패해도 앱 동작에는 영향 없도록 모든 예외 무시
    /// </summary>
    public static class Logger
    {
        private static readonly string LogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "StayAwake",
            "error.log");

        private static readonly Lock _lock = new();
        private const long MaxFileSize = 512 * 1024; // 512KB 초과 시 롤오버

        /// <summary>
        /// 예외 로그 기록 — tag로 호출 지점 식별
        /// </summary>
        public static void LogException(string tag, Exception ex)
        {
            try
            {
                Write($"[ERR] {tag}: {ex.GetType().Name} — {ex.Message}");
            }
            catch { }
        }

        /// <summary>
        /// 일반 경고 로그
        /// </summary>
        public static void LogWarn(string tag, string message)
        {
            try
            {
                Write($"[WARN] {tag}: {message}");
            }
            catch { }
        }

        private static void Write(string message)
        {
            lock (_lock)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);

                // 파일 크기 초과 시 롤오버 (.old로 백업, 하나만 유지)
                if (File.Exists(LogPath))
                {
                    var info = new FileInfo(LogPath);
                    if (info.Length > MaxFileSize)
                    {
                        var backup = LogPath + ".old";
                        try { if (File.Exists(backup)) File.Delete(backup); } catch { }
                        try { File.Move(LogPath, backup); } catch { }
                    }
                }

                var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {message}{Environment.NewLine}";
                File.AppendAllText(LogPath, line);
            }
        }
    }
}
