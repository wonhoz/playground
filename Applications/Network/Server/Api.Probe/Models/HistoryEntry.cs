namespace ApiProbe.Models;

public record HistoryEntry(
    string Summary,
    string Timestamp,
    string Body,
    string Headers);
