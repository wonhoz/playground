using System.ComponentModel;
using System.IO;
using System.Text.RegularExpressions;

namespace Music.Player.Services
{
    public class LrcLine : INotifyPropertyChanged
    {
        private bool _isActive;

        public TimeSpan Time { get; set; }
        public string Text { get; set; } = "";

        public bool IsActive
        {
            get => _isActive;
            set
            {
                if (_isActive != value)
                {
                    _isActive = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsActive)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public static class LrcParser
    {
        private static readonly Regex TimeTagRegex =
            new(@"\[(\d{1,2}):(\d{2})\.(\d{1,3})\]", RegexOptions.Compiled);

        public static List<LrcLine> ParseFile(string lrcFilePath)
        {
            var lines = new List<LrcLine>();
            try
            {
                foreach (var rawLine in File.ReadAllLines(lrcFilePath))
                {
                    var match = TimeTagRegex.Match(rawLine);
                    if (!match.Success) continue;

                    int minutes = int.Parse(match.Groups[1].Value);
                    int seconds = int.Parse(match.Groups[2].Value);
                    string msStr = match.Groups[3].Value.PadRight(3, '0')[..3];
                    int ms = int.Parse(msStr);

                    var time = TimeSpan.FromMilliseconds(minutes * 60000 + seconds * 1000 + ms);
                    var text = rawLine[(match.Index + match.Length)..].Trim();

                    if (!string.IsNullOrWhiteSpace(text))
                        lines.Add(new LrcLine { Time = time, Text = text });
                }
            }
            catch { }

            return lines.OrderBy(l => l.Time).ToList();
        }

        public static string? FindLrcFile(string audioFilePath)
        {
            var lrcPath = Path.ChangeExtension(audioFilePath, ".lrc");
            return File.Exists(lrcPath) ? lrcPath : null;
        }
    }
}
