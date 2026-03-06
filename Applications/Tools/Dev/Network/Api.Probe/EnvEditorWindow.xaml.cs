using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using ApiProbe.Models;
using ApiProbe.Services;

namespace ApiProbe;

/// <summary>환경 변수 편집기에서 사용하는 키/값 행 모델</summary>
public class EnvVarItem : INotifyPropertyChanged
{
    private string _key   = "";
    private string _value = "";

    public string Key
    {
        get => _key;
        set { _key = value; OnPropertyChanged(nameof(Key)); }
    }

    public string Value
    {
        get => _value;
        set { _value = value; OnPropertyChanged(nameof(Value)); }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged(string name) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public partial class EnvEditorWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── 상태 ──────────────────────────────────────────────────────
    private readonly ObservableCollection<EnvPreset> _obsPresets;
    private EnvPreset?                               _currentPreset;
    private ObservableCollection<EnvVarItem>         _vars = [];

    public EnvEditorWindow(IEnumerable<EnvPreset> presets)
    {
        InitializeComponent();
        _obsPresets = new ObservableCollection<EnvPreset>(presets);
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        var dark   = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));

        LstEnv.ItemsSource = _obsPresets;
        if (_obsPresets.Count > 0)
            LstEnv.SelectedIndex = 0;
    }

    // ── 환경 선택 ─────────────────────────────────────────────────
    private void LstEnv_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        SaveCurrentVars();

        var idx = LstEnv.SelectedIndex;
        var hasSelection = idx >= 0 && idx < _obsPresets.Count;

        TxtEnvName.IsEnabled  = hasSelection;
        VarGrid.IsEnabled     = hasSelection;
        BtnAddVar.IsEnabled   = hasSelection;
        BtnDeleteVar.IsEnabled = hasSelection;

        if (!hasSelection)
        {
            _currentPreset = null;
            TxtEnvName.Text    = "";
            VarGrid.ItemsSource = null;
            return;
        }

        _currentPreset = _obsPresets[idx];
        TxtEnvName.Text = _currentPreset.Name;

        _vars = new ObservableCollection<EnvVarItem>(
            _currentPreset.Variables.Select(kv => new EnvVarItem { Key = kv.Key, Value = kv.Value }));
        VarGrid.ItemsSource = _vars;
    }

    // ── 환경 이름 편집 ────────────────────────────────────────────
    private void TxtEnvName_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_currentPreset is null) return;
        var name = TxtEnvName.Text.Trim();
        if (string.IsNullOrEmpty(name)) return;
        _currentPreset.Name = name;
        LstEnv.Items.Refresh();  // DataTemplate 표시 갱신
    }

    // ── 환경 추가/삭제 ────────────────────────────────────────────
    private void AddEnv(object sender, RoutedEventArgs e)
    {
        var preset = new EnvPreset { Name = "새 환경" };
        _obsPresets.Add(preset);
        LstEnv.SelectedIndex = _obsPresets.Count - 1;
    }

    private void DeleteEnv(object sender, RoutedEventArgs e)
    {
        var idx = LstEnv.SelectedIndex;
        if (idx < 0) return;

        _obsPresets.RemoveAt(idx);
        if (_obsPresets.Count > 0)
            LstEnv.SelectedIndex = Math.Min(idx, _obsPresets.Count - 1);
    }

    // ── 변수 추가/삭제 ────────────────────────────────────────────
    private void AddVar(object sender, RoutedEventArgs e)
    {
        if (_currentPreset is null) return;
        var item = new EnvVarItem { Key = "", Value = "" };
        _vars.Add(item);
        VarGrid.ScrollIntoView(item);
        VarGrid.SelectedItem = item;
    }

    private void DeleteVar(object sender, RoutedEventArgs e)
    {
        if (VarGrid.SelectedItem is EnvVarItem item)
            _vars.Remove(item);
    }

    // ── 저장 후 닫기 ──────────────────────────────────────────────
    private void Close_Click(object sender, RoutedEventArgs e)
    {
        VarGrid.CommitEdit(DataGridEditingUnit.Row, true);
        SaveCurrentVars();
        EnvService.Save(_obsPresets);
        Close();
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────
    private void SaveCurrentVars()
    {
        if (_currentPreset is null) return;
        _currentPreset.Variables = _vars
            .Where(v => !string.IsNullOrWhiteSpace(v.Key))
            .ToDictionary(v => v.Key, v => v.Value);
    }
}
