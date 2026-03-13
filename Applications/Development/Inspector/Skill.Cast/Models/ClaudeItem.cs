using System.Collections.Generic;

namespace SkillCast.Models;

public enum ItemType { Command, Skill, Memory, Plugin, Hook, Mcp, Settings }
public enum ItemSource { Global, Project }

public class ClaudeItem
{
    public string Name { get; set; } = "";
    public string FilePath { get; set; } = "";
    public ItemType Type { get; set; }
    public ItemSource Source { get; set; }
    public string Content { get; set; } = "";
    public Dictionary<string, string> Frontmatter { get; set; } = new();

    public string Description => Frontmatter.TryGetValue("description", out var d) ? d : "";
    public string AllowedTools => Frontmatter.TryGetValue("allowed-tools", out var t) ? t : "";
    public string Model => Frontmatter.TryGetValue("model", out var m) ? m : "";
    public string ArgumentHint => Frontmatter.TryGetValue("argument-hint", out var a) ? a : "";

    public string TypeIcon => Type switch
    {
        ItemType.Command => "⚡",
        ItemType.Skill   => "🧠",
        ItemType.Memory  => "💾",
        ItemType.Plugin  => "🔌",
        ItemType.Hook    => "🪝",
        ItemType.Mcp     => "🌐",
        ItemType.Settings => "⚙️",
        _                => "📄"
    };

    public string SourceLabel => Source == ItemSource.Global ? "전역" : "프로젝트";
    public string TypeLabel => Type.ToString();
}

public class PluginInfo
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string License { get; set; } = "";
    public string[] Keywords { get; set; } = [];
    public string FolderPath { get; set; } = "";
    public int CommandCount { get; set; }
    public int SkillCount { get; set; }
    public int AgentCount { get; set; }
    public string ReadmeContent { get; set; } = "";
}

public class KnowledgeArticle
{
    public string Title { get; set; } = "";
    public string Icon { get; set; } = "📚";
    public string Category { get; set; } = "";
    public string Content { get; set; } = "";
}
