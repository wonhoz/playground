namespace GitStats.Models;

public record CommitInfo(
    string Hash,
    string Author,
    string Email,
    string Message,
    DateTimeOffset When,
    int Additions,
    int Deletions,
    int FilesChanged
);

public record DayActivity(DateOnly Date, int Count);

public record HotFile(string Path, int Changes, int Additions, int Deletions);

public record AuthorStat(string Name, string Email, int Commits, int Additions, int Deletions);

public record LanguageStat(string Extension, long Lines, string Color);

public record KeywordStat(string Word, int Count);
