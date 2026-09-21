using AIPForge.Models;
using AIPForge.Services;
using AIPForge.Views;

namespace AIPForge;

/// <summary>개요 화면 — 제품 정보 편집 · 빌드 출력 · 동기화 폴더 · 통계 · 빌드 전 점검</summary>
public partial class MainWindow
{
    private bool _suppressOverview;
    private (AipDocument doc, CheckReport result)? _validation;
    private CancellationTokenSource? _validateCts;

    /// <summary>개요 통계 타일 (라벨, 컴포넌트)</summary>
    private static readonly (string Label, string Cid)[] StatTiles =
    [
        ("파일", "MsiFilesComponent"),
        ("구성 요소", "MsiCompsComponent"),
        ("디렉터리", "MsiDirsComponent"),
        ("바로가기", "MsiShortsComponent"),
        ("레지스트리", "MsiRegsComponent"),
        ("서비스", "MsiServInstComponent"),
        ("사용자 지정 작업", "MsiCustActComponent"),
        ("선행 조건", "PreReqComponent"),
        ("기능", "MsiFeatsComponent"),
        ("실행 조건", "MsiLaunchConditionsComponent"),
    ];

    private void RenderOverview()
    {
        if (_doc == null) return;
        _suppressOverview = true;

        TxtOvVersion.Text = _doc.ProductVersion;
        SetOvField(TxtOvProductName);
        SetOvField(TxtOvArpName);
        SetOvField(TxtOvManufacturer);
        TxtOvUpgradeCode.Text = _doc.GetProperty("UpgradeCode") ?? "(없음)";
        TxtOvProductCode.Text = _doc.FirstProductCode() ?? "(없음)";

        var installed = AdvInstaller.InstalledVersion(_cliPath);
        var project = $"Advanced Installer {_doc.AiVersion} · {_doc.Modules} · 기본 언어 {_doc.Root.Get("Language")}";
        if (installed != null && installed != _doc.AiVersion)
            project += $"\n⚠ 설치된 버전({installed})과 다릅니다 — 빌드하면 프로젝트가 {installed} 형식으로 변환될 수 있습니다.";
        TxtOvProject.Text = project;

        RenderBuilds();
        RenderSyncFolders();
        RenderStats();
        RenderValidation();

        _suppressOverview = false;
    }

    private void SetOvField(TextBox box)
    {
        var prop = (string)box.Tag;
        var value = _doc?.GetProperty(prop);
        box.Text = value ?? "";
        box.IsEnabled = value != null;
        box.ToolTip = value == null ? $"이 프로젝트에는 {prop} 속성이 없습니다." : prop;
    }

    private void OvField_LostFocus(object sender, KeyboardFocusChangedEventArgs e) => ApplyOverviewFields();

    private void OvField_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter) ApplyOverviewFields();
    }

    /// <summary>개요 입력칸 값이 Property와 다르면 반영</summary>
    private void ApplyOverviewFields()
    {
        if (_doc == null || _suppressOverview) return;
        var changed = false;
        foreach (var box in new[] { TxtOvProductName, TxtOvArpName, TxtOvManufacturer })
        {
            if (!box.IsEnabled) continue;
            var prop = (string)box.Tag;
            var value = box.Text.Trim();
            if (_doc.GetProperty(prop) == value) continue;
            if (_doc.SetProperty(prop, value)) changed = true;
        }
        if (!changed) return;
        MarkDirty();
        InvalidateTables();
        RefreshCurrentFolderItem();
        SetStatus("제품 정보 변경됨 — 저장해야 파일에 반영됩니다.");
    }

    private void BtnCopyCode_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        var value = (string)((Button)sender).Tag == "ProductCode" ? _doc.FirstProductCode() : _doc.GetProperty("UpgradeCode");
        if (string.IsNullOrEmpty(value)) return;
        Clipboard.SetText(value);
        SetStatus($"복사됨: {value}");
    }

    private void BtnNewProductCode_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        if (!DarkDialog.Confirm(this,
                "ProductCode를 새로 발급할까요?\n\n버전을 올리지 않고 ProductCode만 바꾸면 같은 버전의 두 제품으로 인식될 수 있으니, 보통은 '버전 변경'에서 함께 바꾸는 것을 권장합니다.",
                "새로 발급"))
            return;
        var code = _doc.RegenerateProductCode();
        if (code == null) return;
        MarkDirty();
        InvalidateTables();
        RenderOverview();
        SetStatus($"ProductCode 새로 발급됨: {code}");
    }

    // ── 버전 변경 ───────────────────────────────────────────────────

    private void BtnVersion_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        if (!CommitPendingEdits()) return;
        var old = _doc.ProductVersion;
        var dlg = new VersionDialog($"{Path.GetFileName(_doc.FilePath)} — {_doc.ProductName}\n현재 버전: {old}",
            old, _settings.NewProductCodeOnVersionChange) { Owner = this };
        if (dlg.ShowDialog() != true) return;

        _settings.NewProductCodeOnVersionChange = dlg.NewProductCode;
        _settings.Save();
        if (!_doc.SetVersion(dlg.Version, dlg.NewProductCode))
        {
            DarkDialog.Info(this, "ProductVersion 속성을 찾지 못했습니다.");
            return;
        }
        MarkDirty();
        InvalidateTables();
        if (_current?.Kind == NavKind.Overview) RenderOverview();
        RefreshCurrentFolderItem();
        SetStatus($"버전 {old} → {dlg.Version}" + (dlg.NewProductCode ? " (ProductCode 새로 발급)" : "") + " — 저장해야 파일에 반영됩니다.");
    }

    // ── 빌드 출력 · 동기화 · 통계 ───────────────────────────────────

    private void RenderBuilds()
    {
        OvBuilds.Children.Clear();
        var builds = _doc!.Builds().ToList();
        if (builds.Count == 0)
        {
            OvBuilds.Children.Add(Hint("빌드 설정(BuildComponent)이 없습니다."));
            return;
        }

        foreach (var b in builds)
        {
            var grid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var info = new StackPanel();
            info.Children.Add(new TextBlock
            {
                Text = $"{b.Name}   ·   {b.Platform}   ·   언어 {Shorten(b.Languages, 60)}",
                FontWeight = FontWeights.SemiBold,
                FontSize = 13,
            });
            info.Children.Add(Mono($"출력 파일  {b.OutputFileName}", 3));

            var folderLine = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
            folderLine.Children.Add(Docked(new TextBlock { Text = "출력 폴더  ", FontSize = 12.5, Foreground = (Brush)FindResource("TextSecondary") }));
            if (b.ResolvedFolder != null)
            {
                var link = LinkTo(b.ResolvedFolder, b.ResolvedFolder);
                link.IsEnabled = System.IO.Directory.Exists(b.ResolvedFolder);
                folderLine.Children.Add(link);
            }
            else folderLine.Children.Add(Mono(b.PackageFolder, 0));
            info.Children.Add(folderLine);

            if (b.OutputPath != null && File.Exists(b.OutputPath))
            {
                var fi = new FileInfo(b.OutputPath);
                info.Children.Add(Hint($"최근 빌드 결과: {fi.LastWriteTime:yyyy-MM-dd HH:mm} · {fi.Length / 1024.0 / 1024.0:N1} MB", 3));
            }
            else info.Children.Add(Hint("현재 버전의 빌드 결과가 아직 없습니다.", 3));

            var btn = new Button { Content = "▶ 이 구성 빌드", Style = (Style)FindResource("AccentButton"), VerticalAlignment = VerticalAlignment.Top };
            var buildName = b.Name;
            btn.Click += async (_, _) => await BuildCurrentAsync(rebuild: false, buildName);
            btn.IsEnabled = !_building;
            Grid.SetColumn(btn, 1);

            grid.Children.Add(info);
            grid.Children.Add(btn);
            OvBuilds.Children.Add(grid);
        }
    }

    private void RenderSyncFolders()
    {
        OvSync.Children.Clear();
        var syncs = _doc!.SyncFolders().ToList();
        if (syncs.Count == 0)
        {
            OvSync.Children.Add(Hint("동기화 폴더가 없습니다. (파일이 프로젝트에 직접 등록됨)"));
            return;
        }
        foreach (var s in syncs)
        {
            var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 10) };
            panel.Children.Add(new TextBlock { Text = $"설치 폴더: {s.Directory}   ·   기능: {s.Feature}", FontSize = 12.5 });
            var line = new DockPanel { Margin = new Thickness(0, 3, 0, 0) };
            line.Children.Add(Docked(new TextBlock { Text = "원본  ", FontSize = 12.5, Foreground = (Brush)FindResource("TextSecondary") }));
            if (s.ResolvedPath != null)
            {
                var exists = System.IO.Directory.Exists(s.ResolvedPath);
                if (!exists)
                    line.Children.Add(Docked(new TextBlock { Text = "   (폴더 없음)", Foreground = (Brush)FindResource("DangerBrush"), FontSize = 12.5 }, Dock.Right));
                var link = LinkTo(s.ResolvedPath, s.ResolvedPath);
                link.IsEnabled = exists;
                line.Children.Add(link);
            }
            else line.Children.Add(Mono(s.SourcePath, 0));
            panel.Children.Add(line);
            OvSync.Children.Add(panel);
        }
    }

    private void RenderStats()
    {
        OvStats.Children.Clear();
        foreach (var (label, cid) in StatTiles)
        {
            var comp = _doc!.FindComponent(cid);
            if (comp == null) continue;
            var count = AipDocument.Rows(comp).Count();
            var content = new StackPanel { MinWidth = 92 };
            content.Children.Add(new TextBlock
            {
                Text = count.ToString("N0"),
                FontSize = 20,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
                HorizontalAlignment = HorizontalAlignment.Left,
            });
            content.Children.Add(new TextBlock { Text = label, FontSize = 12, Foreground = (Brush)FindResource("TextSecondary") });
            var tile = new Button
            {
                Content = content,
                Padding = new Thickness(14, 8, 14, 8),
                Margin = new Thickness(0, 0, 8, 8),
                HorizontalContentAlignment = HorizontalAlignment.Left,
                ToolTip = $"{ComponentCatalog.Get(cid).Title} 테이블 열기",
            };
            tile.Click += (_, _) => { if (_navItems.TryGetValue(comp, out var item)) SelectNav(item); };
            OvStats.Children.Add(tile);
        }
    }

    // ── 빌드 전 점검 ────────────────────────────────────────────────

    private async void BtnValidate_Click(object sender, RoutedEventArgs e)
    {
        if (_doc == null) return;
        if (!CommitPendingEdits()) return;

        _validateCts?.Cancel();
        _validateCts = new CancellationTokenSource();
        var ct = _validateCts.Token;

        // 백그라운드 점검 중 편집이 겹치지 않도록 현재 상태의 사본으로 검사
        var snapshot = AipDocument.Parse(_doc.Serialize());
        snapshot.FilePath = _doc.FilePath;
        var doc = _doc;

        BtnValidate.IsEnabled = false;
        OvValidation.Children.Clear();
        OvValidation.Children.Add(Hint("점검 중... (네트워크 폴더는 시간이 걸릴 수 있습니다)"));
        try
        {
            var result = await Task.Run(() => ProjectValidator.Run(snapshot, ct), ct);
            if (!ReferenceEquals(doc, _doc)) return;
            _validation = (doc, result);
            RenderValidation();
        }
        catch (OperationCanceledException)
        {
            // 다른 파일을 열면서 취소됨
        }
        catch (Exception ex)
        {
            OvValidation.Children.Clear();
            OvValidation.Children.Add(Hint($"점검 실패: {ex.Message}"));
        }
        finally
        {
            BtnValidate.IsEnabled = true;
        }
    }

    private void RenderValidation()
    {
        OvValidation.Children.Clear();
        if (_validation is not { } v || !ReferenceEquals(v.doc, _doc)) return;
        var r = v.result;

        var ok = r.Missing.Count == 0 && r.NewInSyncFolders.Count == 0 && r.Notes.Count == 0;
        OvValidation.Children.Add(new TextBlock
        {
            Text = ok
                ? $"✓ 문제 없음 — 원본 파일 {r.CheckedFiles:N0}개 모두 존재, 동기화 폴더와 일치"
                : $"원본 파일 {r.CheckedFiles:N0}개 확인 · 누락 {r.Missing.Count:N0}개 · 동기화 대기 새 파일 {r.NewInSyncFolders.Count:N0}개",
            Foreground = (Brush)FindResource(ok ? "SuccessBrush" : "AccentBrush"),
            FontWeight = FontWeights.SemiBold,
            FontSize = 13,
        });

        foreach (var note in r.Notes)
            OvValidation.Children.Add(new TextBlock { Text = "⚠ " + note, Foreground = (Brush)FindResource("DangerBrush"), FontSize = 12.5, Margin = new Thickness(0, 6, 0, 0) });

        if (r.Missing.Count > 0)
        {
            OvValidation.Children.Add(new TextBlock
            {
                Text = "누락된 원본 파일 — 빌드가 실패합니다",
                Foreground = (Brush)FindResource("DangerBrush"),
                Margin = new Thickness(0, 10, 0, 4),
                FontSize = 12.5,
            });
            foreach (var m in r.Missing.Take(60)) OvValidation.Children.Add(Mono(m.ResolvedPath ?? m.SourcePath, 1));
            if (r.Missing.Count > 60) OvValidation.Children.Add(Hint($"… 외 {r.Missing.Count - 60}개"));
        }

        if (r.NewInSyncFolders.Count > 0)
        {
            OvValidation.Children.Add(new TextBlock
            {
                Text = "동기화 폴더에 있지만 프로젝트에 없는 파일 — 이대로 빌드하면 설치 패키지에 빠집니다",
                Foreground = (Brush)FindResource("AccentBrush"),
                Margin = new Thickness(0, 10, 0, 4),
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
            });
            foreach (var f in r.NewInSyncFolders.Take(60)) OvValidation.Children.Add(Mono(f, 1));
            if (r.NewInSyncFolders.Count > 60) OvValidation.Children.Add(Hint($"… 외 {r.NewInSyncFolders.Count - 60}개"));

            var sync = new Button
            {
                Content = "지금 폴더 동기화 (/RefreshSync)",
                Style = (Style)FindResource("AccentButton"),
                Margin = new Thickness(0, 10, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                ToolTip = "Advanced Installer CLI로 동기화 폴더를 다시 읽어 프로젝트에 반영합니다 (파일이 저장됩니다)",
            };
            sync.Click += async (_, _) => await RefreshSyncAsync();
            OvValidation.Children.Add(sync);
            OvValidation.Children.Add(Hint("또는 도구 모음의 '빌드 전 폴더 동기화'를 켜 두면 빌드할 때마다 자동으로 동기화합니다.", 4));
        }
    }

    // ── 작은 UI 도우미 ──────────────────────────────────────────────

    private TextBlock Hint(string text, double top = 0) => new()
    {
        Text = text,
        Foreground = (Brush)FindResource("TextSecondary"),
        FontSize = 12.5,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, top, 0, 0),
    };

    private TextBlock Mono(string text, double top) => new()
    {
        Text = text,
        FontFamily = (FontFamily)FindResource("MonoFont"),
        FontSize = 12,
        Margin = new Thickness(0, top, 0, 0),
        TextTrimming = TextTrimming.CharacterEllipsis,
        ToolTip = text,
    };

    private Button LinkTo(string text, string path)
    {
        var btn = new Button { Content = text, Style = (Style)FindResource("LinkButton"), ToolTip = "탐색기에서 열기" };
        btn.Click += (_, _) => OpenInExplorer(path);
        return btn;
    }

    private static T Docked<T>(T element, Dock dock = Dock.Left) where T : UIElement
    {
        DockPanel.SetDock(element, dock);
        return element;
    }

    private static string Shorten(string text, int max) => text.Length <= max ? text : text[..max] + "…";
}
