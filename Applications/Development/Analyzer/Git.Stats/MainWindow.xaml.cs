using System.IO;
using System.Text;
using System.Windows;
using GitStats.ViewModels;
using Microsoft.Win32;

namespace GitStats;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _vm;
        Loaded += (_, _) => App.ApplyDarkTitleBar(this);
    }

    private async void BtnOpen_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = "Git 저장소 폴더 선택" };
        if (dialog.ShowDialog() != true) return;
        await _vm.AnalyzeAsync(dialog.FolderName);
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog
        {
            Title = "Markdown 리포트 저장",
            Filter = "Markdown 파일|*.md",
            FileName = $"git-stats-{DateTime.Now:yyyyMMdd}"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine($"# Git.Stats 리포트 — {_vm.RepoName}");
        sb.AppendLine($"> 생성일: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        sb.AppendLine("## 요약");
        sb.AppendLine($"| 항목 | 값 |");
        sb.AppendLine($"|------|-----|");
        sb.AppendLine($"| 총 커밋 | {_vm.TotalCommits:N0} |");
        sb.AppendLine($"| 기여자 | {_vm.TotalAuthors} |");
        sb.AppendLine();
        sb.AppendLine("## 핫 파일 Top 10");
        sb.AppendLine("| 파일 | 변경 횟수 | 추가 | 삭제 |");
        sb.AppendLine("|------|-----------|------|------|");
        foreach (var f in _vm.HotFiles.Take(10))
            sb.AppendLine($"| `{f.Path}` | {f.Changes} | {f.Additions} | {f.Deletions} |");
        sb.AppendLine();
        sb.AppendLine("## 기여자");
        sb.AppendLine("| 이름 | 커밋 | 추가 | 삭제 |");
        sb.AppendLine("|------|------|------|------|");
        foreach (var a in _vm.AuthorStats)
            sb.AppendLine($"| {a.Name} | {a.Commits} | {a.Additions} | {a.Deletions} |");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
        MessageBox.Show($"리포트가 저장되었습니다:\n{dlg.FileName}", "Git.Stats");
    }
}
