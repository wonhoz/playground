namespace Key.Map.Models;

sealed class ShortcutEntry
{
    public string Keys        { get; set; } = "";  // "Ctrl+Shift+P"
    public string Description { get; set; } = "";
    public string Category    { get; set; } = "";  // File, Edit, View, Run, Navigate, Other
}
