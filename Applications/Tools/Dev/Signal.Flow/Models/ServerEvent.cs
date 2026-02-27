namespace SignalFlow.Models;

public record ServerEvent(
    string   Id,
    string   Type,       // "notify" | "update" | "warning" | "error"
    string   Message,
    string   Source,     // "manual" | "auto"
    DateTime Timestamp
);
