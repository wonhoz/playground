using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using SkillCast.Models;

namespace SkillCast.Services;

public class ClaudeFileService
{
    public string GlobalClaudePath { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude");

    public string? ProjectClaudePath { get; private set; }

    public void SetProjectPath(string projectRoot)
    {
        var candidate = Path.Combine(projectRoot, ".claude");
        ProjectClaudePath = Directory.Exists(candidate) ? candidate : null;
    }

    // ─── Commands ─────────────────────────────────────────────────────────
    public List<ClaudeItem> LoadCommands()
    {
        var items = new List<ClaudeItem>();
        LoadMdFiles(Path.Combine(GlobalClaudePath, "commands"), ItemType.Command, ItemSource.Global, items);
        if (ProjectClaudePath != null)
            LoadMdFiles(Path.Combine(ProjectClaudePath, "commands"), ItemType.Command, ItemSource.Project, items);
        return items;
    }

    // ─── Skills ───────────────────────────────────────────────────────────
    public List<ClaudeItem> LoadSkills()
    {
        var items = new List<ClaudeItem>();
        LoadSkillFiles(Path.Combine(GlobalClaudePath, "skills"), ItemSource.Global, items);
        if (ProjectClaudePath != null)
            LoadSkillFiles(Path.Combine(ProjectClaudePath, "skills"), ItemSource.Project, items);
        return items;
    }

    private void LoadSkillFiles(string dir, ItemSource source, List<ClaudeItem> items)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var skillDir in Directory.GetDirectories(dir))
        {
            var skillMd = Path.Combine(skillDir, "SKILL.md");
            if (File.Exists(skillMd))
                items.Add(ParseMdFile(skillMd, ItemType.Skill, source));
        }
    }

    // ─── Memory ───────────────────────────────────────────────────────────
    public List<ClaudeItem> LoadMemories(string? projectRoot = null)
    {
        var items = new List<ClaudeItem>();

        // Global MEMORY.md
        var globalMemory = Path.Combine(GlobalClaudePath, "MEMORY.md");
        if (File.Exists(globalMemory))
        {
            // Try project-specific memory dir
            if (projectRoot != null)
            {
                var encoded = projectRoot.Replace("\\", "-").Replace("/", "-").Replace(":", "-").Replace("+", "-");
                var memDir = Path.Combine(GlobalClaudePath, "projects", encoded, "memory");
                if (!Directory.Exists(memDir))
                {
                    // Fallback: search for matching project folder
                    var projectsDir = Path.Combine(GlobalClaudePath, "projects");
                    if (Directory.Exists(projectsDir))
                    {
                        foreach (var d in Directory.GetDirectories(projectsDir))
                        {
                            var mem = Path.Combine(d, "memory");
                            if (Directory.Exists(mem))
                            {
                                LoadMdFiles(mem, ItemType.Memory, ItemSource.Project, items);
                                break;
                            }
                        }
                    }
                }
                else
                {
                    LoadMdFiles(memDir, ItemType.Memory, ItemSource.Project, items);
                }
            }

            // Global project memory
            var globalMemIndex = new ClaudeItem
            {
                Name = "MEMORY.md (Index)",
                FilePath = globalMemory,
                Type = ItemType.Memory,
                Source = ItemSource.Global,
                Content = File.ReadAllText(globalMemory, Encoding.UTF8)
            };
            items.Insert(0, globalMemIndex);
        }

        return items;
    }

    // ─── Plugins ──────────────────────────────────────────────────────────
    public List<PluginInfo> LoadPlugins()
    {
        var result = new List<PluginInfo>();
        var pluginsRoot = Path.Combine(GlobalClaudePath, "plugins");
        if (!Directory.Exists(pluginsRoot)) return result;

        // Search recursively for plugin.json in .claude-plugin directories
        foreach (var pluginJson in Directory.GetFiles(pluginsRoot, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                var info = ParsePluginJson(pluginJson);
                result.Add(info);
            }
            catch { }
        }

        return result;
    }

    private PluginInfo ParsePluginJson(string jsonPath)
    {
        var json = File.ReadAllText(jsonPath, Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        var pluginDir = Path.GetDirectoryName(Path.GetDirectoryName(jsonPath)!)!;

        var info = new PluginInfo { FolderPath = pluginDir };

        if (root.TryGetProperty("name", out var n)) info.Name = n.GetString() ?? "";
        if (root.TryGetProperty("version", out var v)) info.Version = v.GetString() ?? "";
        if (root.TryGetProperty("description", out var d)) info.Description = d.GetString() ?? "";
        if (root.TryGetProperty("license", out var l)) info.License = l.GetString() ?? "";

        if (root.TryGetProperty("author", out var a))
        {
            info.Author = a.ValueKind == JsonValueKind.String
                ? a.GetString() ?? ""
                : a.TryGetProperty("name", out var an) ? an.GetString() ?? "" : "";
        }

        if (root.TryGetProperty("keywords", out var kw) && kw.ValueKind == JsonValueKind.Array)
            info.Keywords = kw.EnumerateArray().Select(x => x.GetString() ?? "").ToArray();

        // Count commands / skills / agents
        var cmdDir = Path.Combine(pluginDir, "commands");
        if (Directory.Exists(cmdDir))
            info.CommandCount = Directory.GetFiles(cmdDir, "*.md", SearchOption.AllDirectories).Length;

        var skillDir = Path.Combine(pluginDir, "skills");
        if (Directory.Exists(skillDir))
            info.SkillCount = Directory.GetFiles(skillDir, "SKILL.md", SearchOption.AllDirectories).Length;

        var agentDir = Path.Combine(pluginDir, "agents");
        if (Directory.Exists(agentDir))
            info.AgentCount = Directory.GetFiles(agentDir, "*.md", SearchOption.AllDirectories).Length;

        // Read README
        var readme = Path.Combine(pluginDir, "README.md");
        if (File.Exists(readme))
            info.ReadmeContent = File.ReadAllText(readme, Encoding.UTF8);

        return info;
    }

    // ─── Hooks & MCP ──────────────────────────────────────────────────────
    public List<ClaudeItem> LoadHooksAndMcp()
    {
        var items = new List<ClaudeItem>();

        // Global settings.json
        var settingsFile = Path.Combine(GlobalClaudePath, "settings.json");
        if (File.Exists(settingsFile))
            items.Add(new ClaudeItem
            {
                Name = "settings.json",
                FilePath = settingsFile,
                Type = ItemType.Settings,
                Source = ItemSource.Global,
                Content = PrettyJson(settingsFile)
            });

        // Global .mcp.json
        var mcpFile = Path.Combine(GlobalClaudePath, ".mcp.json");
        if (File.Exists(mcpFile))
            items.Add(new ClaudeItem
            {
                Name = ".mcp.json",
                FilePath = mcpFile,
                Type = ItemType.Mcp,
                Source = ItemSource.Global,
                Content = PrettyJson(mcpFile)
            });

        // Project hooks and mcp
        if (ProjectClaudePath != null)
        {
            var projSettings = Path.Combine(ProjectClaudePath, "settings.json");
            if (File.Exists(projSettings))
                items.Add(new ClaudeItem
                {
                    Name = "settings.json (프로젝트)",
                    FilePath = projSettings,
                    Type = ItemType.Settings,
                    Source = ItemSource.Project,
                    Content = PrettyJson(projSettings)
                });

            var projMcp = Path.Combine(ProjectClaudePath, ".mcp.json");
            if (File.Exists(projMcp))
                items.Add(new ClaudeItem
                {
                    Name = ".mcp.json (프로젝트)",
                    FilePath = projMcp,
                    Type = ItemType.Mcp,
                    Source = ItemSource.Project,
                    Content = PrettyJson(projMcp)
                });

            // Hooks from hooks dir
            var hooksDir = Path.Combine(ProjectClaudePath, "hooks");
            if (Directory.Exists(hooksDir))
            {
                foreach (var json in Directory.GetFiles(hooksDir, "*.json"))
                    items.Add(new ClaudeItem
                    {
                        Name = Path.GetFileName(json),
                        FilePath = json,
                        Type = ItemType.Hook,
                        Source = ItemSource.Project,
                        Content = PrettyJson(json)
                    });
            }
        }

        return items;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────
    private void LoadMdFiles(string dir, ItemType type, ItemSource source, List<ClaudeItem> items)
    {
        if (!Directory.Exists(dir)) return;
        foreach (var f in Directory.GetFiles(dir, "*.md", SearchOption.AllDirectories))
            items.Add(ParseMdFile(f, type, source));
    }

    public ClaudeItem ParseMdFile(string path, ItemType type, ItemSource source)
    {
        var raw = File.ReadAllText(path, Encoding.UTF8);
        var frontmatter = new Dictionary<string, string>();
        var content = raw;

        if (raw.StartsWith("---"))
        {
            var end = raw.IndexOf("---", 3);
            if (end > 0)
            {
                var fmText = raw[3..end].Trim();
                foreach (var line in fmText.Split('\n'))
                {
                    var idx = line.IndexOf(':');
                    if (idx > 0)
                    {
                        var key = line[..idx].Trim();
                        var val = line[(idx + 1)..].Trim();
                        frontmatter[key] = val;
                    }
                }
                content = raw[(end + 3)..].TrimStart();
            }
        }

        var nameWithoutExt = Path.GetFileNameWithoutExtension(path);
        return new ClaudeItem
        {
            Name = nameWithoutExt,
            FilePath = path,
            Type = type,
            Source = source,
            Content = content,
            Frontmatter = frontmatter
        };
    }

    private string PrettyJson(string path)
    {
        try
        {
            var raw = File.ReadAllText(path, Encoding.UTF8);
            var doc = JsonDocument.Parse(raw);
            return JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }

    public void SaveContent(ClaudeItem item, string newContent)
    {
        // Rebuild file with frontmatter if it had one
        if (item.Frontmatter.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("---");
            foreach (var kv in item.Frontmatter)
                sb.AppendLine($"{kv.Key}: {kv.Value}");
            sb.AppendLine("---");
            sb.AppendLine();
            sb.Append(newContent);
            File.WriteAllText(item.FilePath, sb.ToString(), new UTF8Encoding(true));
        }
        else
        {
            File.WriteAllText(item.FilePath, newContent, new UTF8Encoding(true));
        }
        item.Content = newContent;
    }
}
