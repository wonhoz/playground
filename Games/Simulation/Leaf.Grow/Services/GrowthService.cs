namespace LeafGrow.Services;

/// <summary>식물 성장 상태 관리 서비스</summary>
public class GrowthService
{
    public GrowthState State { get; private set; }

    public event Action? GrowthUpdated;

    // 마지막으로 알려진 캔버스 크기 (RebuildAt에서 갱신)
    private double _canvasW = 0;
    private double _canvasH = 0;

    // 성장 히스토리 (뒤로 가기용 스냅샷)
    private readonly Stack<(int Iteration, string LString)> _history = new();
    public bool CanStepBack => _history.Count > 0;

    public GrowthService()
    {
        State = new GrowthState
        {
            Species   = PlantLibrary.All[0],
            Iteration = 0,
            LString   = PlantLibrary.All[0].Axiom,
        };
        Rebuild();
    }

    // ── 종 선택 ───────────────────────────────────────────────────
    public void SelectSpecies(PlantSpecies species)
    {
        _history.Clear();
        State = new GrowthState
        {
            Species   = species,
            Iteration = 0,
            LString   = species.Axiom,
            Sun       = State.Sun,
            Water     = State.Water,
            Nutrients = State.Nutrients,
        };
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 한 단계 성장 ──────────────────────────────────────────────
    public void GrowOne()
    {
        if (State.IsFullyGrown) return;
        _history.Push((State.Iteration, State.LString));
        State.LString = LSystemEngine.Expand(
            State.LString, State.Species.Rules, 1);
        State.Iteration++;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 완전 성장 ────────────────────────────────────────────────
    public void GrowFull()
    {
        int remaining = State.Species.MaxIter - State.Iteration;
        if (remaining <= 0) return;
        _history.Push((State.Iteration, State.LString));
        State.LString = LSystemEngine.Expand(
            State.LString, State.Species.Rules, remaining);
        State.Iteration = State.Species.MaxIter;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 초기화 ───────────────────────────────────────────────────
    public void Reset()
    {
        _history.Clear();
        State.Iteration = 0;
        State.LString   = State.Species.Axiom;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 환경 슬라이더 ─────────────────────────────────────────────
    public void SetEnvironment(double sun, double water, double nutrients)
    {
        State.Sun       = sun;
        State.Water     = water;
        State.Nutrients = nutrients;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 렌더 세그먼트 재빌드 ──────────────────────────────────────
    private void Rebuild()
    {
        // 캔버스 크기가 알려진 경우 사용, 아직 모를 때는 기본값
        double originX = _canvasW > 0 ? _canvasW / 2 : 400;
        double originY = _canvasH > 0 ? _canvasH * 0.92 : 520;
        State.Segments = LSystemEngine.Render(
            State.Species,
            State.LString,
            originX    : originX,
            originY    : originY,
            baseLength : State.Species.Length,
            growthRate : State.GrowthRate);
    }

    // 외부에서 캔버스 크기를 갱신하고 재빌드
    public void RebuildAt(double canvasW, double canvasH)
    {
        _canvasW = canvasW;
        _canvasH = canvasH;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 한 단계 뒤로 ─────────────────────────────────────────────
    public void StepBack()
    {
        if (_history.Count == 0) return;
        var (iter, lstr) = _history.Pop();
        State.Iteration = iter;
        State.LString   = lstr;
        Rebuild();
        GrowthUpdated?.Invoke();
    }
}
