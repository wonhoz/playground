using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using SkillCast.Helpers;
using SkillCast.Models;
using SkillCast.ViewModels;

namespace SkillCast;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += Window_Loaded;
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        DwmHelper.EnableDarkTitleBar(this);
        _vm.LoadAll();
        BindLists();
        UpdatePathBar();
        PopulateOverview();
        StatusBar.Text = _vm.StatusMessage;
        _vm.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.StatusMessage))
                StatusBar.Text = _vm.StatusMessage;
        };
    }

    private void BindLists()
    {
        CommandList.ItemsSource = _vm.Commands;
        SkillList.ItemsSource = _vm.Skills;
        MemoryList.ItemsSource = _vm.Memories;
        PluginList.ItemsSource = _vm.Plugins;
        ConfigList.ItemsSource = _vm.ConfigItems;
        ArticleList.ItemsSource = _vm.Articles;
        PluginCountLabel.Text = $"🔌 플러그인 {_vm.PluginCount}개 설치됨";
    }

    private void UpdatePathBar()
    {
        GlobalPathText.Text = _vm.GlobalClaudePath;
        ProjectPathText.Text = string.IsNullOrEmpty(_vm.ProjectPath) ? "(선택 안 됨)" : _vm.ProjectPath;
    }

    private void PopulateOverview()
    {
        StatCards.Children.Clear();
        AddStatCard("⚡", "Commands", _vm.CommandCount, "#2A4A8A");
        AddStatCard("🧠", "Skills", _vm.SkillCount, "#2A6A3A");
        AddStatCard("💾", "Memory", _vm.MemoryCount, "#5B3A8A");
        AddStatCard("🔌", "Plugins", _vm.PluginCount, "#8A5B2A");
    }

    private void AddStatCard(string icon, string label, int count, string colorHex)
    {
        var color = (Color)ColorConverter.ConvertFromString(colorHex);
        var card = new Border
        {
            Width = 160,
            Height = 90,
            Margin = new Thickness(0, 0, 16, 16),
            Background = new SolidColorBrush(Color.FromArgb(80, color.R, color.G, color.B)),
            BorderBrush = new SolidColorBrush(Color.FromArgb(180, color.R, color.G, color.B)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(16, 12, 16, 12)
        };
        var sp = new StackPanel();
        sp.Children.Add(new TextBlock
        {
            Text = $"{icon}  {count}",
            FontSize = 24,
            FontWeight = FontWeights.Bold,
            Foreground = Brushes.White
        });
        sp.Children.Add(new TextBlock
        {
            Text = label,
            FontSize = 12,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
        });
        card.Child = sp;
        StatCards.Children.Add(card);
    }

    // ─── 공통 ─────────────────────────────────────────────────────────────

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        _vm.LoadAll();
        BindLists();
        UpdatePathBar();
        PopulateOverview();
    }

    private void SelectProject_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "프로젝트 폴더 선택 (.claude 폴더가 있는 루트)" };
        if (dlg.ShowDialog(this) == true)
        {
            _vm.ProjectPath = dlg.FolderName;
            UpdatePathBar();
            _vm.LoadAll();
            BindLists();
            PopulateOverview();
        }
    }

    private void MainTab_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

    // ─── Commands 탭 ──────────────────────────────────────────────────────

    private void CommandList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CommandList.SelectedItem is not ClaudeItem item)
        {
            CmdDetailPanel.Visibility = Visibility.Collapsed;
            CmdEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        _vm.SelectedItem = item;
        CmdEmptyLabel.Visibility = Visibility.Collapsed;
        CmdDetailPanel.Visibility = Visibility.Visible;

        CmdNameLabel.Text = item.Name;
        CmdPathLabel.Text = item.FilePath;
        CmdContentEditor.Text = item.Content;
        CmdHintLabel.Text = $"💡 슬래시 명령어: /{item.Name}  |  {item.SourceLabel}  |  {item.FilePath}";

        CmdFmWrap.Children.Clear();
        if (item.Frontmatter.Count > 0)
        {
            CmdFmPanel.Visibility = Visibility.Visible;
            foreach (var kv in item.Frontmatter)
                CmdFmWrap.Children.Add(MakeFmBadge(kv.Key, kv.Value));
        }
        else
        {
            CmdFmPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void CmdSearch_Changed(object sender, TextChangedEventArgs e)
    {
        var q = CmdSearchBox.Text.Trim().ToLower();
        CommandList.ItemsSource = string.IsNullOrEmpty(q)
            ? _vm.Commands
            : _vm.Commands.Where(c =>
                c.Name.ToLower().Contains(q) ||
                c.Description.ToLower().Contains(q)).ToList();
    }

    private void NewCommand_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewItemDialog("새 Command 만들기", "Command 이름 (예: review-code)") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var item = _vm.CreateCommandTemplate(dlg.ItemName, dlg.Location);
        CommandList.ItemsSource = _vm.Commands;
        CommandList.SelectedItem = item;
        PopulateOverview();
    }

    private void CopyPath_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
            Clipboard.SetText(_vm.SelectedItem.FilePath);
    }

    private void OpenExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
            OpenInExplorer(_vm.SelectedItem.FilePath);
    }

    private void SaveItem_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem == null) return;
        _vm.EditContent = CmdContentEditor.Text;
        _vm.SaveCurrentItem();
        CmdHintLabel.Text = $"✅ 저장됨: {_vm.SelectedItem.FilePath}";
    }

    // ─── Skills 탭 ────────────────────────────────────────────────────────

    private void SkillList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (SkillList.SelectedItem is not ClaudeItem item)
        {
            SkillDetailPanel.Visibility = Visibility.Collapsed;
            SkillEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        _vm.SelectedItem = item;
        SkillEmptyLabel.Visibility = Visibility.Collapsed;
        SkillDetailPanel.Visibility = Visibility.Visible;

        SkillNameLabel.Text = item.Name;
        SkillPathLabel.Text = item.FilePath;
        SkillContentEditor.Text = item.Content;
        SkillHintLabel.Text = $"💡 Skill 호출: /<skill-name>  |  {item.SourceLabel}  |  {item.FilePath}";

        SkillFmWrap.Children.Clear();
        if (item.Frontmatter.Count > 0)
        {
            SkillFmPanel.Visibility = Visibility.Visible;
            foreach (var kv in item.Frontmatter)
                SkillFmWrap.Children.Add(MakeFmBadge(kv.Key, kv.Value));
        }
        else
        {
            SkillFmPanel.Visibility = Visibility.Collapsed;
        }
    }

    private void SkillSearch_Changed(object sender, TextChangedEventArgs e)
    {
        var q = SkillSearchBox.Text.Trim().ToLower();
        SkillList.ItemsSource = string.IsNullOrEmpty(q)
            ? _vm.Skills
            : _vm.Skills.Where(s =>
                s.Name.ToLower().Contains(q) ||
                s.Description.ToLower().Contains(q)).ToList();
    }

    private void NewSkill_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new NewItemDialog("새 Skill 만들기", "Skill 이름 (예: code-review)") { Owner = this };
        if (dlg.ShowDialog() != true) return;
        var item = _vm.CreateSkillTemplate(dlg.ItemName, dlg.Location);
        SkillList.ItemsSource = _vm.Skills;
        SkillList.SelectedItem = item;
        PopulateOverview();
    }

    private void CopySkillPath_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
            Clipboard.SetText(_vm.SelectedItem.FilePath);
    }

    private void OpenSkillExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
            OpenInExplorer(_vm.SelectedItem.FilePath);
    }

    private void SaveSkill_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem == null) return;
        _vm.EditContent = SkillContentEditor.Text;
        _vm.SaveCurrentItem();
        SkillHintLabel.Text = $"✅ 저장됨: {_vm.SelectedItem.FilePath}";
    }

    // ─── Memory 탭 ────────────────────────────────────────────────────────

    private void MemoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MemoryList.SelectedItem is not ClaudeItem item)
        {
            MemDetailPanel.Visibility = Visibility.Collapsed;
            MemEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        _vm.SelectedItem = item;
        MemEmptyLabel.Visibility = Visibility.Collapsed;
        MemDetailPanel.Visibility = Visibility.Visible;

        MemNameLabel.Text = item.Name;
        MemPathLabel.Text = item.FilePath;
        MemContentEditor.Text = item.Content;
    }

    private void CopyMemPath_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem != null)
            Clipboard.SetText(_vm.SelectedItem.FilePath);
    }

    private void SaveMem_Click(object sender, RoutedEventArgs e)
    {
        if (_vm.SelectedItem == null) return;
        _vm.EditContent = MemContentEditor.Text;
        _vm.SaveCurrentItem();
    }

    // ─── Plugins 탭 ───────────────────────────────────────────────────────

    private void PluginList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PluginList.SelectedItem is not PluginInfo plugin)
        {
            PluginDetailScroll.Visibility = Visibility.Collapsed;
            PluginEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        _vm.SelectedPlugin = plugin;
        PluginEmptyLabel.Visibility = Visibility.Collapsed;
        PluginDetailScroll.Visibility = Visibility.Visible;
        BuildPluginDetail(plugin);
    }

    private void BuildPluginDetail(PluginInfo plugin)
    {
        PluginDetailPanel.Children.Clear();

        // 이름·버전·작성자
        var header = new StackPanel { Margin = new Thickness(0, 0, 0, 16) };
        header.Children.Add(new TextBlock
        {
            Text = plugin.Name,
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = FindResource("FgBrush") as Brush
        });
        if (!string.IsNullOrEmpty(plugin.Version) || !string.IsNullOrEmpty(plugin.Author))
            header.Children.Add(new TextBlock
            {
                Text = string.Join("  |  ", new[] { $"v{plugin.Version}", plugin.Author, plugin.License }
                    .Where(s => !string.IsNullOrWhiteSpace(s))),
                FontSize = 12,
                Foreground = FindResource("Fg2Brush") as Brush,
                Margin = new Thickness(0, 4, 0, 0)
            });
        if (!string.IsNullOrEmpty(plugin.Description))
            header.Children.Add(new TextBlock
            {
                Text = plugin.Description,
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 8, 0, 0),
                Foreground = FindResource("FgBrush") as Brush
            });
        PluginDetailPanel.Children.Add(header);

        // 통계
        var statRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
        AddPluginStatBadge(statRow, "⚡ Commands", plugin.CommandCount);
        AddPluginStatBadge(statRow, "🧠 Skills", plugin.SkillCount);
        AddPluginStatBadge(statRow, "🤖 Agents", plugin.AgentCount);
        PluginDetailPanel.Children.Add(statRow);

        // 키워드
        if (plugin.Keywords.Length > 0)
        {
            var kwRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 16) };
            foreach (var kw in plugin.Keywords)
                kwRow.Children.Add(new Border
                {
                    Margin = new Thickness(0, 0, 6, 6),
                    Padding = new Thickness(8, 3, 8, 3),
                    CornerRadius = new CornerRadius(12),
                    Background = FindResource("Bg3Brush") as Brush,
                    Child = new TextBlock
                    {
                        Text = kw,
                        FontSize = 11,
                        Foreground = FindResource("Fg2Brush") as Brush
                    }
                });
            PluginDetailPanel.Children.Add(kwRow);
        }

        // README
        if (!string.IsNullOrEmpty(plugin.ReadmeContent))
        {
            PluginDetailPanel.Children.Add(new TextBlock
            {
                Text = "README",
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = FindResource("Fg2Brush") as Brush,
                Margin = new Thickness(0, 0, 0, 8)
            });
            PluginDetailPanel.Children.Add(new TextBox
            {
                Text = plugin.ReadmeContent,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                Background = FindResource("Bg2Brush") as Brush,
                Foreground = FindResource("FgBrush") as Brush,
                BorderThickness = new Thickness(1),
                BorderBrush = FindResource("BorderBrush") as Brush,
                Padding = new Thickness(14),
                FontFamily = new FontFamily("Segoe UI"),
                FontSize = 12,
                MaxHeight = 500,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto
            });
        }
    }

    private static void AddPluginStatBadge(WrapPanel panel, string label, int count)
    {
        panel.Children.Add(new Border
        {
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(12, 6, 12, 6),
            CornerRadius = new CornerRadius(5),
            Background = new SolidColorBrush(Color.FromRgb(0x38, 0x38, 0x38)),
            Child = new TextBlock
            {
                Text = $"{label}: {count}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xA0))
            }
        });
    }

    // ─── Config 탭 ────────────────────────────────────────────────────────

    private void ConfigList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ConfigList.SelectedItem is not ClaudeItem item)
        {
            CfgDetailPanel.Visibility = Visibility.Collapsed;
            CfgEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        CfgEmptyLabel.Visibility = Visibility.Collapsed;
        CfgDetailPanel.Visibility = Visibility.Visible;
        CfgNameLabel.Text = $"{item.TypeIcon}  {item.Name}";
        CfgPathLabel.Text = item.FilePath;
        CfgContentViewer.Text = item.Content;
    }

    private void OpenCfgExplorer_Click(object sender, RoutedEventArgs e)
    {
        if (ConfigList.SelectedItem is ClaudeItem item)
            OpenInExplorer(item.FilePath);
    }

    // ─── 지식 베이스 탭 ───────────────────────────────────────────────────

    private void ArticleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ArticleList.SelectedItem is not KnowledgeArticle article)
        {
            ArticleDetailPanel.Visibility = Visibility.Collapsed;
            ArticleEmptyLabel.Visibility = Visibility.Visible;
            return;
        }
        ArticleEmptyLabel.Visibility = Visibility.Collapsed;
        ArticleDetailPanel.Visibility = Visibility.Visible;
        ArticleIconLabel.Text = article.Icon;
        ArticleTitleLabel.Text = article.Title;
        ArticleContent.Text = article.Content;
    }

    // ─── 공통 헬퍼 ───────────────────────────────────────────────────────

    private static Border MakeFmBadge(string key, string val)
    {
        return new Border
        {
            Margin = new Thickness(0, 0, 8, 4),
            Padding = new Thickness(8, 3, 8, 3),
            CornerRadius = new CornerRadius(4),
            Background = new SolidColorBrush(Color.FromRgb(0x2D, 0x2D, 0x2D)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x4A, 0x4A, 0x4A)),
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = $"{key}: {val}",
                FontSize = 11,
                FontFamily = new FontFamily("Cascadia Code, Consolas"),
                Foreground = new SolidColorBrush(Color.FromRgb(0x7B, 0xAA, 0xF8))
            }
        };
    }

    private static void OpenInExplorer(string filePath)
    {
        if (File.Exists(filePath))
            Process.Start("explorer.exe", $"/select,\"{filePath}\"");
        else if (Directory.Exists(filePath))
            Process.Start("explorer.exe", $"\"{filePath}\"");
        else if (Path.GetDirectoryName(filePath) is { } dir && Directory.Exists(dir))
            Process.Start("explorer.exe", $"\"{dir}\"");
    }
}
