namespace ChordStrike.Services;

public enum GameState { Menu, Playing, Paused, Result }

public class GameEngine
{
    // ── 이벤트 ─────────────────────────────────────────────────────
    public event Action<Note, Judgment>? NoteJudged;
    public event Action?                 ChartFinished;

    // ── 설정 ───────────────────────────────────────────────────────
    public const double LaneHeight    = 520;   // 레인 화면 높이(px)
    public const double HitY          = 480;   // 히트 라인 Y (하단에서)
    public const double SpeedPx       = 280;   // 노트 기본 낙하 속도 (px/초)
    public const double PerfectWindow = 0.065; // ±65ms
    public const double GoodWindow    = 0.120; // ±120ms

    // ── 상태 ───────────────────────────────────────────────────────
    public GameState    State   { get; private set; } = GameState.Menu;
    public Chart?       Chart   { get; private set; }
    public ScoreResult  Score   { get; private set; } = new();
    public List<Note>   Active  { get; } = [];   // 현재 화면 위 노트
    public bool[] LaneActive    { get; } = new bool[Lanes.Count];

    private double  _beatTime;    // 한 박자 시간 (초)
    private double  _elapsed;     // 현재 재생 경과 시간 (초)
    private double  _lookAhead;   // 노트 생성 선행 시간
    private int     _nextNoteIdx;
    private List<Note> _allNotes = [];

    // ── 시작 ──────────────────────────────────────────────────────
    public void StartChart(Chart chart)
    {
        Chart        = chart;
        Score        = new ScoreResult();
        Active.Clear();
        _elapsed     = 0;
        _nextNoteIdx = 0;
        _beatTime    = 60.0 / chart.BPM;
        _lookAhead   = LaneHeight / SpeedPx + 0.5; // 노트가 화면에 진입할 여유
        _allNotes    = [..chart.Notes.OrderBy(n => n.Beat)];
        State        = GameState.Playing;
    }

    // ── 메인 업데이트 ─────────────────────────────────────────────
    public void Update(double dt)
    {
        if (State != GameState.Playing) return;
        _elapsed += dt;

        double currentBeat = _elapsed / _beatTime;

        // 새 노트 스폰
        while (_nextNoteIdx < _allNotes.Count &&
               _allNotes[_nextNoteIdx].Beat <= currentBeat + _lookAhead / _beatTime)
        {
            var note = _allNotes[_nextNoteIdx++];
            note.Y     = -30;
            note.Judged = false;
            Active.Add(note);
        }

        // 노트 위치 업데이트
        foreach (var note in Active)
        {
            double beatDiff = currentBeat - note.Beat;
            note.Y = HitY - beatDiff * _beatTime * SpeedPx;
        }

        // Miss 판정 (히트 라인 통과 후 0.15초)
        foreach (var note in Active.Where(n => !n.Judged && n.Y > HitY + SpeedPx * 0.15))
        {
            Emit(note, Judgment.Miss);
        }

        // 화면 밖 노트 제거
        Active.RemoveAll(n => n.Judged && n.Y > LaneHeight + 60);

        // 채보 완료 확인
        if (_nextNoteIdx >= _allNotes.Count && Active.All(n => n.Judged))
        {
            State = GameState.Result;
            ChartFinished?.Invoke();
        }
    }

    // ── 키 입력 처리 ──────────────────────────────────────────────
    public void KeyDown(int lane)
    {
        if (State != GameState.Playing) return;
        LaneActive[lane] = true;

        double currentBeat = _elapsed / _beatTime;

        var candidate = Active
            .Where(n => n.Lane == lane && !n.Judged)
            .OrderBy(n => Math.Abs(n.Beat - currentBeat))
            .FirstOrDefault();

        if (candidate == null) return;

        double timeDiff = Math.Abs((candidate.Beat - currentBeat) * _beatTime);
        Judgment j;
        if (timeDiff <= PerfectWindow) j = Judgment.Perfect;
        else if (timeDiff <= GoodWindow) j = Judgment.Good;
        else return; // 범위 밖

        Emit(candidate, j);
    }

    public void KeyUp(int lane)
    {
        if (lane >= 0 && lane < Lanes.Count)
            LaneActive[lane] = false;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────
    private void Emit(Note note, Judgment j)
    {
        note.Judged    = true;
        note.LastJudge = j;

        switch (j)
        {
            case Judgment.Perfect: Score.Perfect++;  Score.Combo++; break;
            case Judgment.Good:    Score.Good++;     Score.Combo++; break;
            case Judgment.Miss:    Score.Miss++;     Score.Combo = 0; break;
        }
        if (Score.Combo > Score.MaxCombo) Score.MaxCombo = Score.Combo;

        NoteJudged?.Invoke(note, j);
    }

    public double CurrentBeat  => _elapsed / _beatTime;
    public double BeatTimeSec  => _beatTime;
    public double Progress     => Chart == null || _allNotes.Count == 0
        ? 0 : (double)_nextNoteIdx / _allNotes.Count;
}
