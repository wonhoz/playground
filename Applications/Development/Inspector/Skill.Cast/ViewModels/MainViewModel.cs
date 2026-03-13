using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using SkillCast.Models;
using SkillCast.Services;

namespace SkillCast.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private readonly ClaudeFileService _svc = new();

    // ─── 탭별 컬렉션 ─────────────────────────────────────────────────────
    public ObservableCollection<ClaudeItem> Commands { get; } = [];
    public ObservableCollection<ClaudeItem> Skills { get; } = [];
    public ObservableCollection<ClaudeItem> Memories { get; } = [];
    public ObservableCollection<PluginInfo> Plugins { get; } = [];
    public ObservableCollection<ClaudeItem> ConfigItems { get; } = [];
    public ObservableCollection<KnowledgeArticle> Articles { get; } = [];

    // ─── 선택된 항목 ─────────────────────────────────────────────────────
    private ClaudeItem? _selectedItem;
    public ClaudeItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            _selectedItem = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(EditContent));
            OnPropertyChanged(nameof(HasFrontmatter));
        }
    }

    private PluginInfo? _selectedPlugin;
    public PluginInfo? SelectedPlugin
    {
        get => _selectedPlugin;
        set { _selectedPlugin = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasPlugin)); }
    }

    private KnowledgeArticle? _selectedArticle;
    public KnowledgeArticle? SelectedArticle
    {
        get => _selectedArticle;
        set { _selectedArticle = value; OnPropertyChanged(); }
    }

    public bool HasSelection => _selectedItem != null;
    public bool HasPlugin => _selectedPlugin != null;
    public bool HasFrontmatter => _selectedItem?.Frontmatter.Count > 0;

    private string _editContent = "";
    public string EditContent
    {
        get => _selectedItem?.Content ?? "";
        set
        {
            _editContent = value;
            OnPropertyChanged();
        }
    }

    // ─── 검색 ─────────────────────────────────────────────────────────────
    private string _searchText = "";
    public string SearchText
    {
        get => _searchText;
        set { _searchText = value; OnPropertyChanged(); ApplySearch(); }
    }

    private void ApplySearch()
    {
        // 검색은 각 탭에서 별도로 필터링 - 뷰에서 처리
    }

    // ─── 상태 ─────────────────────────────────────────────────────────────
    private string _statusMessage = "준비";
    public string StatusMessage
    {
        get => _statusMessage;
        set { _statusMessage = value; OnPropertyChanged(); }
    }

    private string _projectPath = "";
    public string ProjectPath
    {
        get => _projectPath;
        set
        {
            _projectPath = value;
            OnPropertyChanged();
            _svc.SetProjectPath(value);
        }
    }

    public string GlobalClaudePath => _svc.GlobalClaudePath;

    // ─── 통계 ─────────────────────────────────────────────────────────────
    public int CommandCount => Commands.Count;
    public int SkillCount => Skills.Count;
    public int MemoryCount => Memories.Count;
    public int PluginCount => Plugins.Count;

    // ─── 로드 ─────────────────────────────────────────────────────────────
    public void LoadAll()
    {
        LoadCommands();
        LoadSkills();
        LoadMemories();
        LoadPlugins();
        LoadConfig();
        LoadKnowledge();
        StatusMessage = $"Commands: {CommandCount} | Skills: {SkillCount} | Memory: {MemoryCount} | Plugins: {PluginCount}";
    }

    public void LoadCommands()
    {
        Commands.Clear();
        foreach (var item in _svc.LoadCommands())
            Commands.Add(item);
        OnPropertyChanged(nameof(CommandCount));
    }

    public void LoadSkills()
    {
        Skills.Clear();
        foreach (var item in _svc.LoadSkills())
            Skills.Add(item);
        OnPropertyChanged(nameof(SkillCount));
    }

    public void LoadMemories()
    {
        Memories.Clear();
        foreach (var item in _svc.LoadMemories(_projectPath))
            Memories.Add(item);
        OnPropertyChanged(nameof(MemoryCount));
    }

    public void LoadPlugins()
    {
        Plugins.Clear();
        foreach (var p in _svc.LoadPlugins())
            Plugins.Add(p);
        OnPropertyChanged(nameof(PluginCount));
    }

    public void LoadConfig()
    {
        ConfigItems.Clear();
        foreach (var item in _svc.LoadHooksAndMcp())
            ConfigItems.Add(item);
    }

    public void LoadKnowledge()
    {
        Articles.Clear();
        foreach (var a in KnowledgeService.GetArticles())
            Articles.Add(a);
    }

    public void SaveCurrentItem()
    {
        if (SelectedItem == null) return;
        _svc.SaveContent(SelectedItem, _editContent);
        StatusMessage = $"저장됨: {SelectedItem.Name}";
    }

    public ClaudeItem CreateCommandTemplate(string name, string location)
    {
        var basePath = location == "global"
            ? Path.Combine(_svc.GlobalClaudePath, "commands")
            : Path.Combine(_svc.ProjectClaudePath ?? _svc.GlobalClaudePath, "commands");

        Directory.CreateDirectory(basePath);
        var filePath = Path.Combine(basePath, $"{name}.md");

        var content = $"""
---
description: {name} 명령어 설명
allowed-tools: Read
---

{name} 작업을 수행하세요.
""";
        File.WriteAllText(filePath, content, new System.Text.UTF8Encoding(true));

        var item = _svc.ParseMdFile(filePath, ItemType.Command,
            location == "global" ? ItemSource.Global : ItemSource.Project);
        Commands.Add(item);
        OnPropertyChanged(nameof(CommandCount));
        return item;
    }

    public ClaudeItem CreateSkillTemplate(string name, string location)
    {
        var basePath = location == "global"
            ? Path.Combine(_svc.GlobalClaudePath, "skills", name)
            : Path.Combine(_svc.ProjectClaudePath ?? _svc.GlobalClaudePath, "skills", name);

        Directory.CreateDirectory(basePath);
        var filePath = Path.Combine(basePath, "SKILL.md");

        var content = $"""
---
name: {name}
description: {name} 스킬 설명. 이 스킬을 언제 사용할지 Claude가 판단.
---

$ARGUMENTS 에 대해 {name} 작업을 수행하세요.

1. 관련 파일 분석
2. 작업 수행
3. 결과 보고
""";
        File.WriteAllText(filePath, content, new System.Text.UTF8Encoding(true));

        var item = _svc.ParseMdFile(filePath, ItemType.Skill,
            location == "global" ? ItemSource.Global : ItemSource.Project);
        Skills.Add(item);
        OnPropertyChanged(nameof(SkillCount));
        return item;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
