namespace HookCast.Services;

public class GameEngine
{
    // ── 이벤트 ─────────────────────────────────────────────────────
    public event Action?              Bite;         // 입질 발생
    public event Action<CatchRecord>? FishCaught;   // 낚시 성공
    public event Action?              FishEscaped;  // 탈출

    // ── 상태 ───────────────────────────────────────────────────────
    public GamePhase        Phase       { get; private set; } = GamePhase.Aiming;
    public List<Fish>       Fish        { get; private set; } = [];
    public List<CatchRecord> CatchLog   { get; } = [];
    public PhysicsEngine    Physics     { get; } = new();
    public Weather          Weather     { get; private set; } = Weather.Sunny;
    public double           CanvasW     { get; set; } = 800;
    public double           CanvasH     { get; set; } = 600;
    public double           WaterY      => CanvasH * 0.42;    // 수면 Y 좌표
    public Vec2             RodTip      { get; private set; }

    // ── 캐스팅 드래그 ─────────────────────────────────────────────
    public Vec2 DragStart  { get; private set; }
    public Vec2 DragCurrent { get; private set; }
    public bool IsDragging  { get; private set; }

    // ── 릴링 ──────────────────────────────────────────────────────
    private Fish?  _hookedFish;
    private double _reelProgress;   // 0 ~ 1
    private double _fightResist;    // 물고기 저항
    private int    _biteTick;       // 입질 타이머

    private static readonly Random Rng = new();

    // ── 초기화 ────────────────────────────────────────────────────
    public void Init(double w, double h)
    {
        CanvasW  = w;
        CanvasH  = h;
        RodTip   = new Vec2(80, CanvasH * 0.35);
        Weather  = (Weather)Rng.Next(0, 3);
        Fish     = FishAI.SpawnFish(Weather, CanvasW, WaterY, CanvasH);
        Physics.Init(RodTip);
        Phase    = GamePhase.Aiming;
    }

    // ── 드래그 (캐스팅 조준) ──────────────────────────────────────
    public void BeginDrag(Vec2 pos)
    {
        if (Phase != GamePhase.Aiming) return;
        DragStart   = pos;
        DragCurrent = pos;
        IsDragging  = true;
    }

    public void UpdateDrag(Vec2 pos)
    {
        if (!IsDragging) return;
        DragCurrent = pos;
    }

    public void EndDrag(Vec2 pos)
    {
        if (!IsDragging) return;
        IsDragging = false;

        // 드래그 반대 방향으로 낚싯줄 발사
        var dir      = DragStart - pos;   // 반전
        var power    = Math.Clamp(dir.Length / 120.0, 0.2, 1.0);
        var velocity = dir.Normalized * power * 14.0;

        Physics.Cast(RodTip, velocity);
        Phase = GamePhase.Flying;
    }

    // ── 챔질 ──────────────────────────────────────────────────────
    public bool TrySetHook()
    {
        if (Phase != GamePhase.Waiting) return false;

        var biting = Fish.FirstOrDefault(f => f.State == FishState.Biting);
        if (biting == null) return false;

        _hookedFish   = biting;
        biting.State  = FishState.Hooked;
        _reelProgress = 0;
        _fightResist  = biting.FightStrength;
        Phase         = GamePhase.FightReel;
        return true;
    }

    // ── 릴링 전진 (Space 또는 클릭 홀드) ─────────────────────────
    public void Reel(double dt)
    {
        if (Phase != GamePhase.FightReel) return;
        _reelProgress += dt * 0.3 - _fightResist * dt * 0.15;
        _reelProgress  = Math.Max(0, _reelProgress);

        if (_reelProgress >= 1.0)
        {
            // 낚시 성공
            var fish = _hookedFish!;
            var rec  = new CatchRecord(DateTime.Now, fish.Species, fish.KorName,
                                       Math.Round(fish.Size, 1), Math.Round(fish.Weight, 2), Weather);
            CatchLog.Add(rec);
            FishCaught?.Invoke(rec);
            RemoveFish(fish);
            _hookedFish = null;
            Phase = GamePhase.Result;
        }
    }

    // ── 메인 업데이트 루프 ─────────────────────────────────────────
    public void Update()
    {
        switch (Phase)
        {
            case GamePhase.Flying:
                Physics.Step(RodTip, inWater: false);
                // 후크가 수면 아래로 들어오면 착수
                if (Physics.HookPos.Y > WaterY)
                {
                    Physics.LandInWater(new Vec2(Physics.HookPos.X, WaterY + 5));
                    Phase = GamePhase.Waiting;
                    _biteTick = 0;
                }
                break;

            case GamePhase.Waiting:
                Physics.Step(RodTip, inWater: true);
                _biteTick++;

                // 물고기 AI 업데이트
                bool hookInWater = Physics.HookPos.Y >= WaterY;
                foreach (var f in Fish)
                {
                    FishAI.UpdateFish(f, Physics.HookPos, hookInWater,
                                      CanvasW, WaterY, CanvasH, out bool biteOccurred);
                    if (biteOccurred) Bite?.Invoke();
                }

                // 일정 시간 지나도 입질 없으면 재캐스팅 가능
                if (_biteTick > 600) Phase = GamePhase.Aiming;
                break;

            case GamePhase.FightReel:
                Physics.Step(RodTip, inWater: false);
                // 물고기 저항 랜덤 증감
                _fightResist = _hookedFish!.FightStrength * (0.8 + 0.4 * Rng.NextDouble());
                break;

            case GamePhase.Result:
                // 2초 후 자동으로 대기 모드
                if (++_biteTick > 120) Reset();
                break;
        }
    }

    // ── 재시작 ────────────────────────────────────────────────────
    public void Reset()
    {
        Phase       = GamePhase.Aiming;
        if (_hookedFish is not null) FishEscaped?.Invoke();
        _hookedFish = null;
        _reelProgress = 0;
        _biteTick     = 0;
        Physics.Init(RodTip);

        // 물고기 리스폰 (잡은 물고기 교체)
        while (Fish.Count < 4)
            Fish.AddRange(FishAI.SpawnFish(Weather, CanvasW, WaterY, CanvasH).Take(1));
    }

    public double ReelProgress => _reelProgress;
    public Fish?  HookedFish   => _hookedFish;

    private void RemoveFish(Fish f) => Fish.Remove(f);
}
