using PadForge.Core;
using PadForge.Models;

namespace PadForge.ViewModels;

public class MappingEditorViewModel : ViewModelBase
{
    private readonly ControllerProfile _profile;
    private readonly ProfileService    _profileService;

    public ControllerProfile Profile => _profile;

    // 매핑 목록 (모든 GamepadInput 열거)
    public ObservableCollection<MappingRowViewModel> Rows { get; } = [];

    private MappingRowViewModel? _selectedRow;
    public MappingRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set => SetField(ref _selectedRow, value, nameof(SelectedRow));
    }

    // 데드존 슬라이더 (0~100 표시용)
    public double LeftDeadzone
    {
        get => _profile.LeftStickDeadzone * 100;
        set { _profile.LeftStickDeadzone = (float)(value / 100); OnPropertyChanged(nameof(LeftDeadzone)); }
    }
    public double RightDeadzone
    {
        get => _profile.RightStickDeadzone * 100;
        set { _profile.RightStickDeadzone = (float)(value / 100); OnPropertyChanged(nameof(RightDeadzone)); }
    }
    public double TriggerDeadzone
    {
        get => _profile.TriggerDeadzone * 100;
        set { _profile.TriggerDeadzone = (float)(value / 100); OnPropertyChanged(nameof(TriggerDeadzone)); }
    }

    public bool ViGEmEnabled
    {
        get => _profile.ViGEmEnabled;
        set { _profile.ViGEmEnabled = value; OnPropertyChanged(nameof(ViGEmEnabled)); }
    }

    // 프로파일 이름 편집
    public string ProfileName
    {
        get => _profile.Name;
        set { _profile.Name = value; OnPropertyChanged(nameof(ProfileName)); }
    }

    public ICommand SaveCommand   { get; }
    public ICommand ClearCommand  { get; }

    public MappingEditorViewModel(ControllerProfile profile, ProfileService profileService)
    {
        _profile        = profile;
        _profileService = profileService;

        SaveCommand  = new RelayCommand(Save);
        ClearCommand = new RelayCommand(ClearSelected, () => SelectedRow is not null);

        BuildRows();
    }

    private void BuildRows()
    {
        Rows.Clear();
        foreach (GamepadInput input in Enum.GetValues<GamepadInput>())
        {
            var existing = _profile.Mappings.FirstOrDefault(m => m.Input == input);
            Rows.Add(new MappingRowViewModel(input, existing, _profile));
        }
    }

    private void Save() => _profileService.Save(_profile);

    private void ClearSelected()
    {
        if (SelectedRow is null) return;
        SelectedRow.ClearMapping();
    }
}

public class MappingRowViewModel : ViewModelBase
{
    private readonly ControllerProfile _profile;

    public GamepadInput Input     { get; }
    public string       InputName => Input.ToString();

    private ButtonMapping? _mapping;

    public string ActionName  => _mapping?.Action.DisplayName ?? "없음";
    public bool   HasMapping  => _mapping is not null && _mapping.Action.Type != ActionType.None;

    // 동작 타입 선택 (UI 바인딩)
    public ActionType SelectedActionType
    {
        get => _mapping?.Action.Type ?? ActionType.None;
        set
        {
            EnsureMapping();
            _mapping!.Action.Type = value;
            OnPropertyChanged(nameof(SelectedActionType));
            OnPropertyChanged(nameof(ActionName));
            OnPropertyChanged(nameof(HasMapping));
            OnPropertyChanged(nameof(IsKeyAction));
            OnPropertyChanged(nameof(IsTextAction));
            OnPropertyChanged(nameof(IsMouseAction));
        }
    }

    public string KeyCode
    {
        get => _mapping?.Action.KeyCode ?? "";
        set
        {
            EnsureMapping();
            _mapping!.Action.KeyCode = value;
            OnPropertyChanged(nameof(KeyCode));
            OnPropertyChanged(nameof(ActionName));
        }
    }

    public string TextValue
    {
        get => _mapping?.Action.Text ?? "";
        set
        {
            EnsureMapping();
            _mapping!.Action.Text = value;
            OnPropertyChanged(nameof(TextValue));
            OnPropertyChanged(nameof(ActionName));
        }
    }

    // 가시성 조건
    public bool IsKeyAction  => SelectedActionType == ActionType.KeyPress || SelectedActionType == ActionType.KeySequence;
    public bool IsTextAction => SelectedActionType == ActionType.TextType;
    public bool IsMouseAction => SelectedActionType == ActionType.MouseButton;

    public IEnumerable<ActionType> ActionTypes => Enum.GetValues<ActionType>();

    public MappingRowViewModel(GamepadInput input, ButtonMapping? existing, ControllerProfile profile)
    {
        Input    = input;
        _profile = profile;
        _mapping = existing;
    }

    private void EnsureMapping()
    {
        if (_mapping is not null) return;
        _mapping = new ButtonMapping { Input = Input };
        _profile.Mappings.Add(_mapping);
    }

    public void ClearMapping()
    {
        if (_mapping is null) return;
        _profile.Mappings.Remove(_mapping);
        _mapping = null;
        OnPropertyChanged(nameof(SelectedActionType));
        OnPropertyChanged(nameof(ActionName));
        OnPropertyChanged(nameof(HasMapping));
    }
}
