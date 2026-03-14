using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using DiffPlex.DiffBuilder;
using DiffPlex.DiffBuilder.Model;
using Microsoft.Win32;
using StrForge.Models;
using StrForge.Services;

namespace StrForge;

public partial class MainWindow : Window
{
    private string _rootPath = string.Empty;
    private readonly List<ReplaceRule> _rules = [];
    private List<FileReplaceResult> _previewResults = [];
    private ReplaceRule? _currentRule;
    private bool _suppressEditorUpdate = false;

    public MainWindow()
    {
        InitializeComponent();
        AddNewRule();
    }

    // ── 다크 타이틀바 ──
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int val = 1;
        DwmSetWindowAttribute(hwnd, 20, ref val, sizeof(int));
    }

    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(nint hwnd, int attr, ref int value, int size);

    // ══════════════════════════════════════════════════════
    //  툴바 버튼
    // ══════════════════════════════════════════════════════

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "폴더 선택" };
        if (dlg.ShowDialog() != true) return;
        LoadFolder(dlg.FolderName);
    }

    private void LoadFolder(string path)
    {
        if (!Directory.Exists(path)) return;
        _rootPath = path;
        RefreshFileList();
        ClearDiff();
        SetStatus($"폴더: {path}");
    }

    private void RefreshFileList()
    {
        var glob = TxtGlob.Text.Trim();
        if (string.IsNullOrEmpty(glob)) glob = "**/*";
        var files = FileScanner.ScanFiles(_rootPath, glob);
        ResultList.ItemsSource = null;
        _previewResults = [];
        LblFileCount.Text = $"{files.Count}개 파일";
        BtnPreview.IsEnabled = files.Count > 0;
        BtnApply.IsEnabled = false;

        // 경로만 표시 (미리보기 전)
        ResultList.ItemsSource = files.Select(f => Path.GetRelativePath(_rootPath, f)).ToList();
    }

    private async void BtnPreview_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_rootPath)) return;
        if (!CompileAllRules()) return;

        var glob = TxtGlob.Text.Trim();
        if (string.IsNullOrEmpty(glob)) glob = "**/*";
        var files = FileScanner.ScanFiles(_rootPath, glob);
        if (files.Count == 0) { SetStatus("대상 파일 없음"); return; }

        BtnPreview.IsEnabled = false;
        BtnApply.IsEnabled = false;
        SetStatus("미리보기 계산 중...");
        ProgressText.Text = "0 / " + files.Count;

        var progress = new Progress<(int current, int total)>(p =>
        {
            ProgressText.Text = $"{p.current} / {p.total}";
        });

        _previewResults = await Task.Run(() =>
            ReplaceEngine.Preview(files, _rootPath, _rules, progress));

        ProgressText.Text = string.Empty;
        BtnPreview.IsEnabled = true;
        BtnApply.IsEnabled = _previewResults.Count > 0;

        ResultList.ItemsSource = _previewResults;
        LblFileCount.Text = $"{_previewResults.Count}개 변경됨 / {files.Count}개 검사";
        SetStatus(_previewResults.Count > 0
            ? $"{_previewResults.Sum(r => r.MatchCount)}개 매치, {_previewResults.Count}개 파일 변경 예정"
            : "변경 없음");
        ClearDiff();
    }

    private void BtnApply_Click(object sender, RoutedEventArgs e)
    {
        if (_previewResults.Count == 0) return;
        var selected = _previewResults.Where(r => r.IsSelected && r.HasChanges).ToList();
        if (selected.Count == 0) { SetStatus("선택된 파일 없음"); return; }

        var backup = ChkBackup.IsChecked == true;
        var msg = $"{selected.Count}개 파일에 치환을 적용합니다" +
                  (backup ? " (.bak 백업 생성)" : "") + ". 계속하시겠습니까?";
        if (MessageBox.Show(msg, "치환 적용", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        var (applied, skipped) = ReplaceEngine.Apply(_previewResults, backup);
        SetStatus($"완료: {applied}개 적용, {skipped}개 실패");
        BtnApply.IsEnabled = false;
        _previewResults = [];
        ResultList.ItemsSource = null;
        ClearDiff();
    }

    private void BtnSavePreset_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "프리셋 저장",
            Filter = "JSON 프리셋|*.json",
            DefaultExt = "json",
            FileName = "preset"
        };
        if (dlg.ShowDialog() != true) return;

        var ruleSet = new RuleSet
        {
            GlobPattern = TxtGlob.Text,
            Rules = [.. _rules]
        };
        var json = JsonSerializer.Serialize(ruleSet, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(dlg.FileName, json, System.Text.Encoding.UTF8);
        SetStatus($"프리셋 저장: {Path.GetFileName(dlg.FileName)}");
    }

    private void BtnLoadPreset_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog
        {
            Title = "프리셋 불러오기",
            Filter = "JSON 프리셋|*.json"
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var ruleSet = JsonSerializer.Deserialize<RuleSet>(json);
            if (ruleSet == null) return;

            _rules.Clear();
            _rules.AddRange(ruleSet.Rules);
            TxtGlob.Text = ruleSet.GlobPattern;
            RefreshRuleList();
            SetStatus($"프리셋 로드: {Path.GetFileName(dlg.FileName)}");
        }
        catch (Exception ex)
        {
            MessageBox.Show($"프리셋 읽기 오류: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // ══════════════════════════════════════════════════════
    //  드래그앤드롭
    // ══════════════════════════════════════════════════════

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var items = (string[])e.Data.GetData(DataFormats.FileDrop);
        var folder = items.FirstOrDefault(Directory.Exists);
        if (folder != null) LoadFolder(folder);
    }

    // ══════════════════════════════════════════════════════
    //  규칙 관리
    // ══════════════════════════════════════════════════════

    private void BtnAddRule_Click(object sender, RoutedEventArgs e) => AddNewRule();

    private void AddNewRule()
    {
        var rule = new ReplaceRule { Label = $"규칙 {_rules.Count + 1}" };
        _rules.Add(rule);
        RefreshRuleList();
        RuleList.SelectedIndex = _rules.Count - 1;
    }

    private void BtnRemoveRule_Click(object sender, RoutedEventArgs e)
    {
        if (RuleList.SelectedIndex < 0) return;
        _rules.RemoveAt(RuleList.SelectedIndex);
        RefreshRuleList();
        if (_rules.Count > 0) RuleList.SelectedIndex = Math.Min(RuleList.SelectedIndex, _rules.Count - 1);
        else { _currentRule = null; ClearEditor(); }
    }

    private void RefreshRuleList()
    {
        RuleList.ItemsSource = null;
        RuleList.ItemsSource = _rules.Select(r =>
        {
            var label = string.IsNullOrEmpty(r.Pattern) ? "(빈 규칙)" : r.Pattern;
            var badge = r.IsEnabled ? "" : " [꺼짐]";
            return label + badge;
        }).ToList();
        BtnRemoveRule.IsEnabled = _rules.Count > 0;
    }

    private void RuleList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        var idx = RuleList.SelectedIndex;
        BtnRemoveRule.IsEnabled = idx >= 0;
        if (idx < 0 || idx >= _rules.Count)
        {
            _currentRule = null;
            ClearEditor();
            return;
        }
        _currentRule = _rules[idx];
        LoadRuleToEditor(_currentRule);
    }

    private void LoadRuleToEditor(ReplaceRule rule)
    {
        _suppressEditorUpdate = true;
        TxtPattern.Text = rule.Pattern;
        TxtReplacement.Text = rule.Replacement;
        ChkRegex.IsChecked = rule.IsRegex;
        ChkIgnoreCase.IsChecked = rule.IgnoreCase;
        ChkWholeWord.IsChecked = rule.WholeWord;
        TxtPatternError.Text = string.Empty;
        _suppressEditorUpdate = false;
    }

    private void ClearEditor()
    {
        _suppressEditorUpdate = true;
        TxtPattern.Text = string.Empty;
        TxtReplacement.Text = string.Empty;
        ChkRegex.IsChecked = false;
        ChkIgnoreCase.IsChecked = false;
        ChkWholeWord.IsChecked = false;
        TxtPatternError.Text = string.Empty;
        _suppressEditorUpdate = false;
    }

    private void TxtPattern_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEditorUpdate || _currentRule == null) return;
        _currentRule.Pattern = TxtPattern.Text;
        UpdateRuleLabel(_currentRule);
        ValidateCurrentRule();
        RefreshRuleList();
    }

    private void TxtReplacement_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suppressEditorUpdate || _currentRule == null) return;
        _currentRule.Replacement = TxtReplacement.Text;
    }

    private void ChkOption_Click(object sender, RoutedEventArgs e)
    {
        if (_currentRule == null) return;
        _currentRule.IsRegex = ChkRegex.IsChecked == true;
        _currentRule.IgnoreCase = ChkIgnoreCase.IsChecked == true;
        _currentRule.WholeWord = ChkWholeWord.IsChecked == true;
        ValidateCurrentRule();
    }

    private void ValidateCurrentRule()
    {
        if (_currentRule == null) return;
        _currentRule.TryCompile();
        TxtPatternError.Text = _currentRule.CompileError ?? string.Empty;
    }

    private static void UpdateRuleLabel(ReplaceRule rule)
    {
        rule.Label = string.IsNullOrEmpty(rule.Pattern) ? "(빈 규칙)" : rule.Pattern;
    }

    private bool CompileAllRules()
    {
        var hasError = false;
        foreach (var r in _rules)
        {
            if (!r.TryCompile())
            {
                hasError = true;
                SetStatus($"규칙 오류: {r.Pattern} — {r.CompileError}");
            }
        }
        return !hasError;
    }

    // ══════════════════════════════════════════════════════
    //  파일 목록 → Diff 미리보기
    // ══════════════════════════════════════════════════════

    private void ResultList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ResultList.SelectedItem is not FileReplaceResult result)
        {
            ClearDiff();
            return;
        }
        LblDiffFile.Text = result.RelativePath;
        RenderDiff(result.OriginalContent, result.NewContent);
    }

    private void RenderDiff(string original, string modified)
    {
        DiffView.Document.Blocks.Clear();
        var diff = InlineDiffBuilder.Diff(original, modified);

        var para = new Paragraph { Margin = new Thickness(0), LineHeight = 1.4 };
        foreach (var line in diff.Lines)
        {
            var prefix = line.Type switch
            {
                ChangeType.Inserted => "+ ",
                ChangeType.Deleted => "- ",
                _ => "  "
            };
            var bg = line.Type switch
            {
                ChangeType.Inserted => new SolidColorBrush(Color.FromRgb(0x1a, 0x3a, 0x1a)),
                ChangeType.Deleted => new SolidColorBrush(Color.FromRgb(0x3a, 0x1a, 0x1a)),
                _ => Brushes.Transparent
            };
            var fg = line.Type switch
            {
                ChangeType.Inserted => new SolidColorBrush(Color.FromRgb(0x88, 0xdd, 0x88)),
                ChangeType.Deleted => new SolidColorBrush(Color.FromRgb(0xdd, 0x88, 0x88)),
                _ => new SolidColorBrush(Color.FromRgb(0xcc, 0xcc, 0xcc))
            };

            var run = new Run(prefix + line.Text + "\n")
            {
                Foreground = fg,
                Background = bg
            };
            para.Inlines.Add(run);
        }
        DiffView.Document.Blocks.Add(para);
    }

    private void ClearDiff()
    {
        DiffView.Document.Blocks.Clear();
        LblDiffFile.Text = "파일 선택 시 Diff 표시";
    }

    // ══════════════════════════════════════════════════════
    //  Glob 패턴 변경
    // ══════════════════════════════════════════════════════

    private void TxtGlob_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded || string.IsNullOrEmpty(_rootPath)) return;
        RefreshFileList();
    }

    // ══════════════════════════════════════════════════════
    //  상태바
    // ══════════════════════════════════════════════════════

    private void SetStatus(string msg) => StatusBar.Text = msg;

    private TextBlock ProgressText => (TextBlock)FindName("ProgressBar");
}
