namespace Layout.Forge.ViewModels;

using Layout.Forge.Models;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    // ── 키보드 행 ────────────────────────────────────────────────────────

    public IReadOnlyList<IReadOnlyList<KeyViewModel>> MainRows { get; }
    public IReadOnlyList<IReadOnlyList<KeyViewModel>> NavRows  { get; }

    // ── 프로파일 ─────────────────────────────────────────────────────────

    public ObservableCollection<KeyProfile> Profiles { get; } = new();

    KeyProfile? _selectedProfile;
    public KeyProfile? SelectedProfile
    {
        get => _selectedProfile;
        set { _selectedProfile = value; ApplyProfileToBoard(); Notify(); }
    }

    // ── 선택 키 ──────────────────────────────────────────────────────────

    KeyViewModel? _selectedKey;
    public KeyViewModel? SelectedKey
    {
        get => _selectedKey;
        set
        {
            if (_selectedKey != null) _selectedKey.IsSelected = false;
            _selectedKey = value;
            if (_selectedKey != null) _selectedKey.IsSelected = true;
            Notify();
            Notify(nameof(HasSelection));
            Notify(nameof(SelectedKeyLabel));
            Notify(nameof(SelectedTarget));
        }
    }

    public bool HasSelection => _selectedKey != null && !_selectedKey.Key.IsSpacer;
    public string SelectedKeyLabel => _selectedKey?.Key.Label ?? "";

    KeyTarget? _selectedTarget;
    public KeyTarget? SelectedTarget
    {
        get => _selectedTarget;
        set { _selectedTarget = value; Notify(); }
    }

    // ── 상태 ─────────────────────────────────────────────────────────────

    string _statusText = "";
    public string StatusText
    {
        get => _statusText;
        set { _statusText = value; Notify(); }
    }

    // ── 현재 시스템 적용 상태 ─────────────────────────────────────────

    public Dictionary<ushort, ushort> ActiveSystemMappings { get; private set; } = new();

    // ── 생성자 ───────────────────────────────────────────────────────────

    public MainViewModel()
    {
        MainRows = KeyboardLayout.MainRows.Select(row =>
            row.Select(k => new KeyViewModel(k)).ToList() as IReadOnlyList<KeyViewModel>).ToList();
        NavRows = KeyboardLayout.NavRows.Select(row =>
            row.Select(k => new KeyViewModel(k)).ToList() as IReadOnlyList<KeyViewModel>).ToList();

        // 기본 프로파일 로드
        var saved = ProfileService.LoadAll();
        if (saved.Count > 0)
        {
            foreach (var p in saved) Profiles.Add(p);
        }
        else
        {
            var def = new KeyProfile { Name = "기본 프로파일" };
            Profiles.Add(def);
            ProfileService.Save(def);
        }

        // 현재 시스템 매핑 읽기
        try { ActiveSystemMappings = ScancodeService.Read(); } catch { }

        SelectedProfile = Profiles[0];
    }

    // ── 보드 업데이트 ─────────────────────────────────────────────────

    void ApplyProfileToBoard()
    {
        var mappings = _selectedProfile?.Mappings ?? new Dictionary<ushort, ushort>();
        foreach (var row in MainRows.Concat(NavRows))
            foreach (var kvm in row)
            {
                kvm.MappedTo = mappings.TryGetValue(kvm.Key.ScanCode, out var t) ? t : null;
            }

        // 선택된 키의 대상도 업데이트
        if (_selectedKey != null)
            SelectedTarget = KeyboardLayout.Targets
                .FirstOrDefault(t => t.ScanCode == _selectedKey.MappedTo);
    }

    // ── 키 선택 ──────────────────────────────────────────────────────────

    public void SelectKey(KeyViewModel kvm)
    {
        if (kvm.Key.IsSpacer) return;
        SelectedKey = kvm;
        SelectedTarget = kvm.IsRemapped
            ? KeyboardLayout.Targets.FirstOrDefault(t => t.ScanCode == kvm.MappedTo)
            : null;
    }

    // ── 리매핑 설정 ──────────────────────────────────────────────────────

    public void SetRemap()
    {
        if (_selectedKey == null || _selectedProfile == null || _selectedTarget == null) return;

        var src = _selectedKey.Key.ScanCode;
        if (_selectedTarget.ScanCode == 0xFFFF) // "변경 없음" sentinel
        {
            _selectedProfile.Mappings.Remove(src);
            _selectedKey.MappedTo = null;
        }
        else
        {
            _selectedProfile.Mappings[src] = _selectedTarget.ScanCode;
            _selectedKey.MappedTo = _selectedTarget.ScanCode;
        }
        StatusText = $"'{_selectedKey.Key.Label}' 리매핑 설정 완료 (프로파일에 저장됨)";
        ProfileService.Save(_selectedProfile);
    }

    public void ClearRemap()
    {
        if (_selectedKey == null || _selectedProfile == null) return;
        _selectedProfile.Mappings.Remove(_selectedKey.Key.ScanCode);
        _selectedKey.MappedTo = null;
        SelectedTarget = null;
        StatusText = $"'{_selectedKey.Key.Label}' 리매핑 제거됨";
        ProfileService.Save(_selectedProfile);
    }

    // ── 프로파일 관리 ─────────────────────────────────────────────────

    public void NewProfile(string name)
    {
        var p = new KeyProfile { Name = name };
        Profiles.Add(p);
        ProfileService.Save(p);
        SelectedProfile = p;
    }

    public void RenameProfile(string newName)
    {
        if (_selectedProfile == null) return;
        ProfileService.Delete(_selectedProfile);
        _selectedProfile.Name = newName;
        ProfileService.Save(_selectedProfile);
        Notify(nameof(SelectedProfile));
    }

    public void DeleteProfile()
    {
        if (_selectedProfile == null || Profiles.Count <= 1) return;
        ProfileService.Delete(_selectedProfile);
        Profiles.Remove(_selectedProfile);
        SelectedProfile = Profiles[0];
    }

    // ── 시스템 적용 ──────────────────────────────────────────────────────

    public void ApplyToSystem()
    {
        if (_selectedProfile == null) return;
        ScancodeService.Apply(_selectedProfile.Mappings);
        ActiveSystemMappings = new Dictionary<ushort, ushort>(_selectedProfile.Mappings);
        StatusText = "✓ 레지스트리에 적용됨. 재부팅 후 활성화됩니다.";
    }

    public void RestoreDefault()
    {
        ScancodeService.Clear();
        ActiveSystemMappings.Clear();
        StatusText = "✓ Scancode Map 제거됨. 재부팅 후 기본값으로 복원됩니다.";
    }

    void Notify([CallerMemberName] string? p = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(p));
}
