namespace PathGuard.Models;

public record PathSnapshot(
    DateTime CreatedAt,
    string   SystemPath,
    string   UserPath,
    string   Label
);
