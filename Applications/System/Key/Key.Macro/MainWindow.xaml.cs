using System.Windows.Threading;
using KeyMacro.Models;
using KeyMacro.Services;
using KeyMacro.Views;

namespace KeyMacro;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<MacroRule> _rules;
    private readonly AppSettings _settings;
    private readonly MacroEngine _engine = new();
    private readonly DispatcherTimer _saveTimer = new() { Interval = TimeSpan.FromMilliseconds(800) };
    private readonly DispatcherTimer _scheduleTimer = new() { Interval = TimeSpan.FromMilliseconds(200) };
    private readonly Dictionary<string, DateTime> _lastScheduledRun = new();

    private MacroRule? _rule;       // 편집 중인 매크로
    private MacroAction? _action;   // 편집 중인 동작
    private bool _binding;          // 상세 패널을 코드로 채우는 중 (핸들러 재진입 방지)

    public MainWindow()
    {
        InitializeComponent();
        DarkTitle.Apply(this);

        _rules = MacroStore.Load();
        foreach (var rule in _rules) Watch(rule);

        _settings = SettingsStore.Load();

        LstRules.ItemsSource = _rules;
        _engine.Rules = _rules;
        _engine.Settings = _settings;
        _engine.Log += message => Dispatcher.BeginInvoke(() => TxtLog.Text = message);
        _engine.StateChanged += () => Dispatcher.BeginInvoke(UpdateStatus);

        _saveTimer.Tick += (_, _) => { _saveTimer.Stop(); SaveNow(); };
        _scheduleTimer.Tick += (_, _) => ScheduleTick();

        Loaded += (_, _) =>
        {
            UpdateRuleCount();
            UpdateStatus();
            UpdateHint();
            if (_rules.Count > 0) LstRules.SelectedIndex = 0;
            _scheduleTimer.Start();
        };

        Closing += (_, _) =>
        {
            _scheduleTimer.Stop();
            _saveTimer.Stop();
            SaveNow();
            SettingsStore.Save(_settings);
            _engine.Dispose();
        };
    }

    // ══════════ 저장 ══════════
    private void ScheduleSave()
    {
        _saveTimer.Stop();
        _saveTimer.Start();
    }

    private void SaveNow()
    {
        try { MacroStore.Save(_rules); }
        catch (Exception ex) { TxtLog.Text = $"저장 실패: {ex.Message}"; }
    }

    /// <summary>동작 개수가 바뀌면 목록 요약도 갱신되도록 구독</summary>
    private void Watch(MacroRule rule)
        => rule.Actions.CollectionChanged += (_, _) => { rule.RaiseDisplay(); ScheduleSave(); };

    // ══════════ 감시 on/off ══════════
    private void BtnToggle_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (_engine.IsActive) _engine.Stop();
            else _engine.Start();
        }
        catch (Exception ex)
        {
            DarkDialog.Info(this, ex.Message, "감시를 시작할 수 없습니다");
        }
        UpdateStatus();
    }

    private void UpdateStatus()
    {
        bool on = _engine.IsActive;
        TxtStatus.Text = _engine.IsBusy ? "매크로 실행 중" : on ? "감시 중" : "감시 꺼짐";
        DotStatus.Fill = (SolidColorBrush)FindResource(
            _engine.IsBusy ? "AccentBrush" : on ? "SuccessBrush" : "TextSecondary");
        BtnToggle.Content = on ? "감시 중지" : "감시 시작";
        BdStatus.Background = new SolidColorBrush(on ? Color.FromRgb(0x25, 0x3B, 0x33)
                                                     : Color.FromRgb(0x2A, 0x2A, 0x38));
    }

    private void UpdateRuleCount() => TxtRuleCount.Text = $"매크로 {_rules.Count}개";

    /// <summary>상태바 오른쪽 안내를 현재 중단 키에 맞춰 갱신</summary>
    private void UpdateHint()
        => TxtHint.Text = $"실행 중 {_settings.AbortDisplay} = 즉시 중단   ·   설정은 자동 저장됩니다";

    // ══════════ 전체 설정 · 시간 동기화 ══════════
    private void BtnOptions_Click(object sender, RoutedEventArgs e)
    {
        _engine.Paused = true;
        try
        {
            if (!OptionsDialog.Show(this, _settings)) return;
        }
        finally { _engine.Paused = false; }

        SettingsStore.Save(_settings);
        UpdateHint();
        TxtLog.Text = $"중단 키: {_settings.AbortDisplay}";
    }

    private void BtnTimeSync_Click(object sender, RoutedEventArgs e)
    {
        _engine.Paused = true;
        try { TimeSyncDialog.Show(this, _settings); }
        finally { _engine.Paused = false; }

        SettingsStore.Save(_settings);
    }

    // ══════════ 예약 실행 ══════════
    private void ChkSchedule_Click(object sender, RoutedEventArgs e)
    {
        if (_binding || _rule is null) return;

        _rule.ScheduleEnabled = ChkSchedule.IsChecked == true;
        if (_rule.ScheduleEnabled && _rule.ScheduleAt is null)
        {
            DarkDialog.Info(this, "시각을 HH:mm:ss 형식으로 입력하세요. (예: 10:10:09)");
            _rule.ScheduleEnabled = false;
            ChkSchedule.IsChecked = false;
        }

        UpdateNextRun();
        ScheduleSave();
    }

    private void ScheduleTime_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _rule is null) return;

        _rule.ScheduleTime = TxtScheduleTime.Text.Trim();
        UpdateNextRun();
        ScheduleSave();
    }

    private void CmbScheduleMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding || _rule is null || CmbScheduleMode.SelectedIndex < 0) return;

        _rule.ScheduleDaily = CmbScheduleMode.SelectedIndex == 0;
        ScheduleSave();
    }

    private void BtnScheduleNow_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;

        var soon = DateTime.Now.AddMinutes(1);
        _binding = true;
        TxtScheduleTime.Text = soon.ToString("HH:mm:ss");
        _binding = false;

        _rule.ScheduleTime = TxtScheduleTime.Text;
        UpdateNextRun();
        ScheduleSave();
    }

    /// <summary>예약 시각이 되면 키 입력 없이 실행한다 (0.2초마다 확인)</summary>
    private void ScheduleTick()
    {
        var now = DateTime.Now;

        foreach (var rule in _rules.ToList())
        {
            if (!rule.Enabled || !rule.HasSchedule) continue;

            var target = now.Date + rule.ScheduleAt!.Value;
            double passed = (now - target).TotalSeconds;
            if (passed < 0 || passed > 2) continue;                       // 지정 시각 직후에만
            if (_lastScheduledRun.TryGetValue(rule.Id, out var last)
                && (now - last).TotalSeconds < 5) continue;               // 같은 시각 중복 실행 방지

            _lastScheduledRun[rule.Id] = now;
            _engine.RunNow(rule);
            TxtLog.Text = $"⏰ 예약 실행: {rule.Name} ({rule.ScheduleTime})";

            if (!rule.ScheduleDaily)
            {
                rule.ScheduleEnabled = false;                             // 한 번만 모드는 실행 후 해제
                if (ReferenceEquals(rule, _rule))
                {
                    _binding = true;
                    ChkSchedule.IsChecked = false;
                    _binding = false;
                }
                ScheduleSave();
            }
        }

        UpdateNextRun();
    }

    /// <summary>편집 중인 매크로의 다음 실행까지 남은 시간 표시</summary>
    private void UpdateNextRun()
    {
        if (_rule is null) return;

        if (!_rule.HasSchedule)
        {
            TxtNextRun.Text = _rule.ScheduleEnabled && _rule.ScheduleAt is null
                ? "시각 형식이 올바르지 않습니다 (HH:mm:ss)"
                : "";
            return;
        }

        var now = DateTime.Now;
        var next = now.Date + _rule.ScheduleAt!.Value;
        if (next <= now) next = next.AddDays(1);

        var left = next - now;
        TxtNextRun.Text = left.TotalHours >= 1
            ? $"다음 실행까지 {(int)left.TotalHours}시간 {left.Minutes}분"
            : left.TotalMinutes >= 1
                ? $"다음 실행까지 {left.Minutes}분 {left.Seconds}초"
                : $"다음 실행까지 {left.Seconds}초";
    }

    // ══════════ 매크로 목록 ══════════
    private void BtnAddRule_Click(object sender, RoutedEventArgs e)
    {
        var rule = new MacroRule { Name = $"매크로 {_rules.Count + 1}" };
        Watch(rule);
        _rules.Add(rule);
        UpdateRuleCount();
        LstRules.SelectedItem = rule;

        // 새로 만들면 곧바로 트리거 키를 받는 것이 가장 자연스럽다
        AssignTrigger(rule);
        ScheduleSave();
    }

    private void LstRules_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded && LstRules.SelectedItem is null) return;

        _rule = LstRules.SelectedItem as MacroRule;
        bool has = _rule is not null;

        PnlEmpty.Visibility  = has ? Visibility.Collapsed : Visibility.Visible;
        PnlEditor.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        BtnDuplicate.IsEnabled = BtnDeleteRule.IsEnabled = has;

        if (_rule is null) return;

        _binding = true;
        TxtTrigger.Text   = _rule.TriggerDisplay;
        TxtName.Text      = _rule.Name;
        ChkSuppress.IsChecked = _rule.SuppressTrigger;
        TxtRepeat.Text    = _rule.RepeatCount.ToString();
        TxtGap.Text       = _rule.GapMs.ToString();
        ChkSchedule.IsChecked = _rule.ScheduleEnabled;
        TxtScheduleTime.Text  = _rule.ScheduleTime;
        CmbScheduleMode.SelectedIndex = _rule.ScheduleDaily ? 0 : 1;
        LstActions.ItemsSource = _rule.Actions;
        _binding = false;

        UpdateNextRun();

        LstActions.SelectedIndex = _rule.Actions.Count > 0 ? 0 : -1;
        UpdateActionPlaceholder();
    }

    private void RuleEnabled_Changed(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        ScheduleSave();
    }

    private void BtnDuplicate_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;

        var copy = new MacroRule
        {
            Name = _rule.Name + " 복사본",
            Enabled = false,                 // 같은 트리거가 겹치지 않도록 꺼둔 채로 복제
            TriggerVk = _rule.TriggerVk,
            Ctrl = _rule.Ctrl, Alt = _rule.Alt, Shift = _rule.Shift, Win = _rule.Win,
            SuppressTrigger = _rule.SuppressTrigger,
            RepeatCount = _rule.RepeatCount,
            GapMs = _rule.GapMs,
            ScheduleTime = _rule.ScheduleTime,
            ScheduleDaily = _rule.ScheduleDaily,
            ScheduleEnabled = false      // 복제본이 곧바로 예약 실행되지 않도록
        };
        foreach (var action in _rule.Actions) copy.Actions.Add(action.Clone());

        Watch(copy);
        _rules.Add(copy);
        UpdateRuleCount();
        LstRules.SelectedItem = copy;
        TxtLog.Text = "복제했습니다 — 트리거가 겹치지 않도록 꺼진 상태입니다";
        ScheduleSave();
    }

    private void BtnDeleteRule_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;
        if (!DarkDialog.Confirm(this, $"'{_rule.Name}' 매크로를 삭제할까요?", "삭제")) return;

        _rules.Remove(_rule);
        UpdateRuleCount();
        LstRules.SelectedIndex = _rules.Count > 0 ? 0 : -1;
        if (_rules.Count == 0) LstRules_SelectionChanged(LstRules, null!);
        ScheduleSave();
    }

    private void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "매크로 파일 (*.json)|*.json",
            FileName = "key-macro.json"
        };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            MacroStore.Export(dialog.FileName, _rules);
            TxtLog.Text = $"내보냈습니다: {dialog.FileName}";
        }
        catch (Exception ex) { DarkDialog.Info(this, ex.Message, "내보내기 실패"); }
    }

    private void BtnImport_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "매크로 파일 (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;

        try
        {
            var imported = MacroStore.Import(dialog.FileName);
            foreach (var rule in imported)
            {
                rule.Id = Guid.NewGuid().ToString("N");
                Watch(rule);
                _rules.Add(rule);
            }
            UpdateRuleCount();
            TxtLog.Text = $"매크로 {imported.Count}개를 불러왔습니다";
            ScheduleSave();
        }
        catch (Exception ex) { DarkDialog.Info(this, ex.Message, "가져오기 실패"); }
    }

    // ══════════ 트리거 ══════════
    private void BtnSetTrigger_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is not null) AssignTrigger(_rule);
    }

    private void AssignTrigger(MacroRule rule)
    {
        if (!Capture("이 매크로를 실행할 키를 눌러 주세요",
                     "Ctrl · Alt · Shift · Win 을 함께 누르면 조합 키가 됩니다", out var combo))
            return;

        var conflict = _rules.FirstOrDefault(r => r != rule
            && r.TriggerVk == combo.Vk && r.Ctrl == combo.Ctrl && r.Alt == combo.Alt
            && r.Shift == combo.Shift && r.Win == combo.Win);

        if (conflict is not null &&
            !DarkDialog.Confirm(this,
                $"'{conflict.Name}' 매크로가 이미 같은 키를 쓰고 있습니다.\n" +
                "그대로 두면 위쪽 매크로만 실행됩니다. 계속할까요?", "계속"))
            return;

        rule.TriggerVk = combo.Vk;
        rule.Ctrl = combo.Ctrl;
        rule.Alt = combo.Alt;
        rule.Shift = combo.Shift;
        rule.Win = combo.Win;

        TxtTrigger.Text = rule.TriggerDisplay;
        TxtLog.Text = $"트리거 키: {rule.TriggerDisplay}";
        ScheduleSave();
    }

    private void TxtName_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _rule is null) return;
        _rule.Name = TxtName.Text;
        ScheduleSave();
    }

    private void ChkSuppress_Click(object sender, RoutedEventArgs e)
    {
        if (_binding || _rule is null) return;
        _rule.SuppressTrigger = ChkSuppress.IsChecked == true;
        ScheduleSave();
    }

    // ══════════ 동작 추가 ══════════
    private void BtnAddClick_Click(object sender, RoutedEventArgs e) => AddPointAction(ActionKind.MouseClick);
    private void BtnAddMove_Click(object sender, RoutedEventArgs e)  => AddPointAction(ActionKind.MouseMove);

    private void AddPointAction(ActionKind kind)
    {
        if (_rule is null) return;
        if (!PickPoint(out int x, out int y)) return;

        Add(new MacroAction { Kind = kind, X = x, Y = y });
        TxtLog.Text = $"지점 ({x}, {y}) 을(를) 추가했습니다";
    }

    private void BtnAddKey_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;
        if (!Capture("입력할 키를 눌러 주세요", "누른 그대로 기록됩니다", out var combo)) return;

        Add(new MacroAction
        {
            Kind = ActionKind.KeyStroke,
            VirtualKey = combo.Vk,
            Ctrl = combo.Ctrl, Alt = combo.Alt, Shift = combo.Shift, Win = combo.Win
        });
    }

    private void BtnAddText_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;
        Add(new MacroAction { Kind = ActionKind.Text, Text = "" });
        TxtActionText.Focus();
    }

    private void BtnAddDelay_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;
        Add(new MacroAction { Kind = ActionKind.Delay, DelayMs = 100 });
    }

    /// <summary>새 동작을 현재 선택 위치 다음에 넣고 바로 선택한다</summary>
    private void Add(MacroAction action)
    {
        if (_rule is null) return;

        int index = LstActions.SelectedIndex;
        if (index >= 0 && index < _rule.Actions.Count - 1) _rule.Actions.Insert(index + 1, action);
        else _rule.Actions.Add(action);

        LstActions.SelectedItem = action;
        LstActions.ScrollIntoView(action);
        UpdateActionPlaceholder();
        ScheduleSave();
    }

    // ══════════ 동작 목록 조작 ══════════
    private void BtnMoveUp_Click(object sender, RoutedEventArgs e) => Move(sender, -1);
    private void BtnMoveDown_Click(object sender, RoutedEventArgs e) => Move(sender, +1);

    private void Move(object sender, int offset)
    {
        if (_rule is null || (sender as Button)?.Tag is not MacroAction action) return;

        int from = _rule.Actions.IndexOf(action);
        int to = from + offset;
        if (from < 0 || to < 0 || to >= _rule.Actions.Count) return;

        _rule.Actions.Move(from, to);
        LstActions.SelectedItem = action;
        ScheduleSave();
    }

    private void BtnRemoveAction_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null || (sender as Button)?.Tag is not MacroAction action) return;

        _rule.Actions.Remove(action);
        UpdateActionPlaceholder();
        ScheduleSave();
    }

    private void UpdateActionPlaceholder()
        => TxtNoAction.Visibility = _rule is { Actions.Count: > 0 } ? Visibility.Collapsed : Visibility.Visible;

    // ══════════ 선택한 동작 상세 ══════════
    private void LstActions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        _action = LstActions.SelectedItem as MacroAction;

        PnlMouse.Visibility = PnlKey.Visibility = PnlText.Visibility = PnlDelay.Visibility = Visibility.Collapsed;
        TxtNoSelection.Visibility = _action is null ? Visibility.Visible : Visibility.Collapsed;
        if (_action is null) return;

        _binding = true;
        switch (_action.Kind)
        {
            case ActionKind.MouseClick:
            case ActionKind.MouseMove:
                PnlMouse.Visibility = Visibility.Visible;
                PnlClickOnly.Visibility = _action.Kind == ActionKind.MouseClick
                    ? Visibility.Visible : Visibility.Collapsed;
                TxtPosX.Text = _action.X.ToString();
                TxtPosY.Text = _action.Y.ToString();
                CmbButton.SelectedIndex = (int)_action.Button;
                TxtClickCount.Text = _action.ClickCount.ToString();
                break;

            case ActionKind.KeyStroke:
                PnlKey.Visibility = Visibility.Visible;
                TxtActionKey.Text = KeyNames.Combo(_action.VirtualKey, _action.Ctrl, _action.Alt,
                                                   _action.Shift, _action.Win);
                CmbKeyMode.SelectedIndex = (int)_action.KeyMode;
                break;

            case ActionKind.Text:
                PnlText.Visibility = Visibility.Visible;
                TxtActionText.Text = _action.Text;
                break;

            case ActionKind.Delay:
                PnlDelay.Visibility = Visibility.Visible;
                TxtDelay.Text = _action.DelayMs.ToString();
                break;
        }
        _binding = false;
    }

    private void Pos_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _action is null) return;
        if (int.TryParse(TxtPosX.Text, out int x)) _action.X = x;
        if (int.TryParse(TxtPosY.Text, out int y)) _action.Y = y;
        ScheduleSave();
    }

    private void BtnRepick_Click(object sender, RoutedEventArgs e)
    {
        if (_action is null) return;
        if (!PickPoint(out int x, out int y)) return;

        _action.X = x;
        _action.Y = y;
        _binding = true;
        TxtPosX.Text = x.ToString();
        TxtPosY.Text = y.ToString();
        _binding = false;
        ScheduleSave();
    }

    private void CmbButton_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding || _action is null || CmbButton.SelectedIndex < 0) return;
        _action.Button = (ClickButton)CmbButton.SelectedIndex;
        ScheduleSave();
    }

    private void ClickCount_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _action is null) return;
        if (int.TryParse(TxtClickCount.Text, out int count) && count > 0) _action.ClickCount = count;
        ScheduleSave();
    }

    private void BtnRecaptureKey_Click(object sender, RoutedEventArgs e)
    {
        if (_action is null) return;
        if (!Capture("입력할 키를 눌러 주세요", "누른 그대로 기록됩니다", out var combo)) return;

        _action.VirtualKey = combo.Vk;
        _action.Ctrl = combo.Ctrl;
        _action.Alt = combo.Alt;
        _action.Shift = combo.Shift;
        _action.Win = combo.Win;
        TxtActionKey.Text = KeyNames.Combo(combo.Vk, combo.Ctrl, combo.Alt, combo.Shift, combo.Win);
        ScheduleSave();
    }

    private void CmbKeyMode_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_binding || _action is null || CmbKeyMode.SelectedIndex < 0) return;
        _action.KeyMode = (KeyStrokeMode)CmbKeyMode.SelectedIndex;
        ScheduleSave();
    }

    private void ActionText_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _action is null) return;
        _action.Text = TxtActionText.Text;
        ScheduleSave();
    }

    private void Delay_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _action is null) return;
        if (int.TryParse(TxtDelay.Text, out int ms) && ms >= 0) _action.DelayMs = ms;
        ScheduleSave();
    }

    private void BtnDelayPreset_Click(object sender, RoutedEventArgs e)
    {
        if (_action is null || (sender as Button)?.Tag is not string tag) return;
        if (!int.TryParse(tag, out int ms)) return;

        _action.DelayMs = ms;
        _binding = true;
        TxtDelay.Text = ms.ToString();
        _binding = false;
        ScheduleSave();
    }

    // ══════════ 실행 설정 ══════════
    private void Repeat_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _rule is null) return;
        if (int.TryParse(TxtRepeat.Text, out int count) && count > 0) _rule.RepeatCount = count;
        ScheduleSave();
    }

    private void Gap_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_binding || _rule is null) return;
        if (int.TryParse(TxtGap.Text, out int ms) && ms >= 0) _rule.GapMs = ms;
        ScheduleSave();
    }

    private void BtnTest_Click(object sender, RoutedEventArgs e)
    {
        if (_rule is null) return;
        if (_rule.Actions.Count == 0)
        {
            DarkDialog.Info(this, "실행할 동작이 없습니다. 먼저 동작을 추가하세요.");
            return;
        }

        var rule = _rule;
        BtnTest.IsEnabled = false;

        int left = 3;
        var countdown = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        BtnTest.Content = $"{left}초 뒤 실행...";

        countdown.Tick += (_, _) =>
        {
            left--;
            if (left > 0)
            {
                BtnTest.Content = $"{left}초 뒤 실행...";
                return;
            }

            countdown.Stop();
            BtnTest.Content = "▶  3초 뒤 테스트 실행";
            BtnTest.IsEnabled = true;
            _engine.RunNow(rule);
        };
        countdown.Start();

        TxtLog.Text = $"3초 안에 매크로를 실행할 창을 클릭해 활성화하세요 (중단은 {_settings.AbortDisplay})";
    }

    // ══════════ 입력 받기 공통 ══════════
    /// <summary>키 캡처 · 지점 지정 동안에는 매크로 감시를 잠시 멈춘다</summary>
    private bool Capture(string header, string hint, out KeyCombo combo)
    {
        _engine.Paused = true;
        try { return KeyCaptureDialog.TryCapture(this, header, hint, out combo); }
        finally { _engine.Paused = false; }
    }

    private bool PickPoint(out int x, out int y)
    {
        _engine.Paused = true;
        try { return PointPickerWindow.TryPick(this, out x, out y); }
        finally { _engine.Paused = false; }
    }
}
