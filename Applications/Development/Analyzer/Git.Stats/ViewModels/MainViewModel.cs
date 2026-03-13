using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using GitStats.Models;
using GitStats.Services;

namespace GitStats.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    private string _repoPath = "";
    private bool _isLoaded;
    private bool _isBusy;
    private int _progress;
    private string _statusText = "폴더를 열어 분석을 시작하세요";
    private int _totalCommits;
    private int _totalAuthors;
    private int _totalFiles;

    public string RepoPath
    {
        get => _repoPath;
        set { _repoPath = value; OnPropertyChanged(); OnPropertyChanged(nameof(RepoName)); }
    }

    public string RepoName => string.IsNullOrEmpty(_repoPath) ? "" : System.IO.Path.GetFileName(_repoPath);

    public bool IsBusy
    {
        get => _isBusy;
        set { _isBusy = value; OnPropertyChanged(); }
    }

    public int Progress
    {
        get => _progress;
        set { _progress = value; OnPropertyChanged(); }
    }

    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; OnPropertyChanged(); }
    }

    public int TotalCommits
    {
        get => _totalCommits;
        set { _totalCommits = value; OnPropertyChanged(); }
    }

    public int TotalAuthors
    {
        get => _totalAuthors;
        set { _totalAuthors = value; OnPropertyChanged(); }
    }

    public int TotalFiles
    {
        get => _totalFiles;
        set { _totalFiles = value; OnPropertyChanged(); }
    }

    public bool IsLoaded
    {
        get => _isLoaded;
        set { _isLoaded = value; OnPropertyChanged(); }
    }

    public ObservableCollection<DayActivity> HeatmapData { get; } = new();
    public ObservableCollection<HotFile> HotFiles { get; } = new();
    public ObservableCollection<AuthorStat> AuthorStats { get; } = new();
    public ObservableCollection<LanguageStat> LanguageStats { get; } = new();
    public ObservableCollection<KeywordStat> KeywordStats { get; } = new();
    public ObservableCollection<CommitInfo> RecentCommits { get; } = new();

    public async Task AnalyzeAsync(string repoPath)
    {
        if (!GitAnalyzer.IsValidRepo(repoPath))
        {
            MessageBox.Show("유효한 Git 저장소가 아닙니다.", "Git.Stats", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        RepoPath = repoPath;
        IsBusy = true;
        IsLoaded = false;
        StatusText = "커밋 히스토리 분석 중...";
        ClearAll();

        try
        {
            var analyzer = new GitAnalyzer(repoPath);
            var progress = new Progress<int>(p => { Progress = p; StatusText = $"커밋 분석 중... {p}%"; });

            var commits = await Task.Run(() => analyzer.GetAllCommits(progress));
            TotalCommits = commits.Count;

            StatusText = "핫 파일 분석 중...";
            var hotFiles = await Task.Run(() => analyzer.GetHotFiles());
            TotalFiles = hotFiles.Count;

            StatusText = "기여자 통계 집계 중...";
            var authors = await Task.Run(() => analyzer.GetAuthorStats(commits));
            TotalAuthors = authors.Count;

            StatusText = "언어 분포 분석 중...";
            var languages = await Task.Run(() => analyzer.GetLanguageStats());

            StatusText = "키워드 분석 중...";
            var keywords = await Task.Run(() => analyzer.GetKeywords(commits));

            StatusText = "히트맵 데이터 생성 중...";
            var heatmap = await Task.Run(() => analyzer.GetHeatmapData(commits));

            // UI 업데이트
            foreach (var d in heatmap) HeatmapData.Add(d);
            foreach (var f in hotFiles) HotFiles.Add(f);
            foreach (var a in authors) AuthorStats.Add(a);
            foreach (var l in languages) LanguageStats.Add(l);
            foreach (var k in keywords) KeywordStats.Add(k);
            foreach (var c in commits.Take(100)) RecentCommits.Add(c);

            IsLoaded = true;
            StatusText = $"분석 완료 — 커밋 {TotalCommits:N0}개 · 기여자 {TotalAuthors}명 · {RepoName}";
        }
        catch (Exception ex)
        {
            StatusText = $"오류: {ex.Message}";
            MessageBox.Show($"분석 중 오류가 발생했습니다.\n{ex.Message}", "Git.Stats", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            Progress = 0;
        }
    }

    private void ClearAll()
    {
        HeatmapData.Clear();
        HotFiles.Clear();
        AuthorStats.Clear();
        LanguageStats.Clear();
        KeywordStats.Clear();
        RecentCommits.Clear();
        TotalCommits = 0;
        TotalAuthors = 0;
        TotalFiles = 0;
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    protected void OnPropertyChanged([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
