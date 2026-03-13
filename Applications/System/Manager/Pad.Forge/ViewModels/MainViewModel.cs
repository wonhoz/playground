using PadForge.Core;
using PadForge.Models;

namespace PadForge.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly ControllerService _controllerService;
    private readonly ProfileService    _profileService;
    private readonly ViGEmService      _vigemService;

    public ObservableCollection<ControllerViewModel> Controllers { get; } = [];
    public ObservableCollection<ControllerProfile>   Profiles    => _profileService.Profiles;

    private ControllerProfile? _selectedProfile;
    public ControllerProfile? SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetField(ref _selectedProfile, value, nameof(SelectedProfile)))
            {
                _profileService.Activate(value!);
                _controllerService.SetProfile(value);
                MappingVm = value is not null
                    ? new MappingEditorViewModel(value, _profileService)
                    : null;
                OnPropertyChanged(nameof(MappingVm));
            }
        }
    }

    private MappingEditorViewModel? _mappingVm;
    public MappingEditorViewModel? MappingVm
    {
        get => _mappingVm;
        private set => SetField(ref _mappingVm, value, nameof(MappingVm));
    }

    public TestViewModel TestVm { get; }

    // ViGEm 상태 표시
    public bool ViGEmAvailable => _vigemService.IsAvailable;
    public string ViGEmStatus  => _vigemService.IsAvailable ? "ViGEm: 활성" : "ViGEm: 드라이버 없음";

    // 명령
    public ICommand NewProfileCommand    { get; }
    public ICommand DeleteProfileCommand { get; }
    public ICommand SaveProfileCommand   { get; }
    public ICommand ExportProfileCommand { get; }
    public ICommand ImportProfileCommand { get; }

    public MainViewModel(ControllerService ctrl, ProfileService profile, ViGEmService vigem)
    {
        _controllerService = ctrl;
        _profileService    = profile;
        _vigemService      = vigem;

        TestVm = new TestViewModel(_controllerService);

        ctrl.ControllerConnected    += OnControllerConnected;
        ctrl.ControllerDisconnected += OnControllerDisconnected;
        ctrl.StateUpdated           += OnStateUpdated;

        profile.ProfileActivated += p => SelectedProfile = p;

        NewProfileCommand    = new RelayCommand(NewProfile);
        DeleteProfileCommand = new RelayCommand(DeleteProfile, () => SelectedProfile is not null);
        SaveProfileCommand   = new RelayCommand(SaveProfile,   () => SelectedProfile is not null);
        ExportProfileCommand = new RelayCommand(ExportProfile, () => SelectedProfile is not null);
        ImportProfileCommand = new RelayCommand(ImportProfile);

        if (Profiles.Count > 0) SelectedProfile = Profiles[0];
    }

    private void OnControllerConnected(ControllerState state)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            if (Controllers.All(c => c.Index != state.Index))
                Controllers.Add(new ControllerViewModel(state));
        });
    }

    private void OnControllerDisconnected(ControllerState state)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var vm = Controllers.FirstOrDefault(c => c.Index == state.Index);
            if (vm is not null) Controllers.Remove(vm);
        });
    }

    private void OnStateUpdated(ControllerState state)
    {
        WpfApplication.Current.Dispatcher.Invoke(() =>
        {
            var vm = Controllers.FirstOrDefault(c => c.Index == state.Index);
            vm?.Update(state);
            TestVm.Update(state);
        });
    }

    private void NewProfile()
    {
        var p = new ControllerProfile { Name = $"프로파일 {Profiles.Count + 1}" };
        _profileService.Profiles.Add(p);
        _profileService.Save(p);
        SelectedProfile = p;
    }

    private void DeleteProfile()
    {
        if (SelectedProfile is null) return;
        _profileService.Delete(SelectedProfile);
        SelectedProfile = Profiles.FirstOrDefault();
    }

    private void SaveProfile()
    {
        if (SelectedProfile is null) return;
        _profileService.Save(SelectedProfile);
    }

    private void ExportProfile()
    {
        if (SelectedProfile is null) return;
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "JSON 파일|*.json",
            FileName = $"{SelectedProfile.Name}.json"
        };
        if (dlg.ShowDialog() == true)
            _profileService.Export(SelectedProfile, dlg.FileName);
    }

    private void ImportProfile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "JSON 파일|*.json" };
        if (dlg.ShowDialog() == true)
        {
            var p = _profileService.Import(dlg.FileName);
            if (p is not null) SelectedProfile = p;
        }
    }
}
