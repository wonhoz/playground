namespace LeafGrow.Services;

/// <summary>식물 성장 상태 관리 서비스</summary>
public class GrowthService
{
    public GrowthState State { get; private set; }

    public event Action? GrowthUpdated;

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
        State.LString = LSystemEngine.Expand(
            State.LString, State.Species.Rules, remaining);
        State.Iteration = State.Species.MaxIter;
        Rebuild();
        GrowthUpdated?.Invoke();
    }

    // ── 초기화 ───────────────────────────────────────────────────
    public void Reset()
    {
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
        // 캔버스 크기는 렌더 시 외부에서 전달하므로 기본값 사용
        State.Segments = LSystemEngine.Render(
            State.Species,
            State.LString,
            originX    : 400,
            originY    : 520,
            baseLength : State.Species.Length,
            growthRate : State.GrowthRate);
    }

    // 외부에서 캔버스 크기 알고 재빌드
    public void RebuildAt(double canvasW, double canvasH)
    {
        State.Segments = LSystemEngine.Render(
            State.Species,
            State.LString,
            originX    : canvasW / 2,
            originY    : canvasH * 0.92,
            baseLength : State.Species.Length,
            growthRate : State.GrowthRate);
        GrowthUpdated?.Invoke();
    }
}
