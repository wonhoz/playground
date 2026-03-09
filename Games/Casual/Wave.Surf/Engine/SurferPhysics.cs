namespace WaveSurf.Engine;

public enum SurferState { OnWave, InAir, Wiping, Wiped }

/// <summary>COM(무게중심) 기반 서퍼 물리 + 공중 묘기 Verlet 통합</summary>
public class SurferPhysics
{
    // 화면 고정 X 위치 (파도가 스크롤, 서퍼는 여기에 고정)
    public double ScreenX => 380.0;

    private const double LaunchVelThreshold = -160.0; // 파도 상승 속도 임계값 (px/s)
    private const double Gravity             = 520.0;  // 중력 가속도 (px/s²)
    private const double MaxAngularVel       = 720.0;  // 최대 회전 속도 (°/s)
    private const double AngularAccel        = 1400.0; // 회전 가속도 (°/s²)
    private const double AngularDamping      = 5.0;    // 회전 감쇠 계수
    private const double LandingTolerance    = 28.0;   // 착지 각도 허용 오차 (°)
    private const double WipeoutBalance      = 1.05;   // 이 이상이면 와이프아웃

    public double ScreenY { get; private set; }

    /// <summary>서퍼 표시 각도 (라디안)</summary>
    public double Angle { get; private set; }

    /// <summary>균형 지수 (-1~+1, 초과 시 와이프아웃)</summary>
    public double Balance { get; private set; }

    public SurferState State { get; private set; } = SurferState.OnWave;

    /// <summary>공중 수직 속도 (캔버스 기준 — 음수 = 상승)</summary>
    public double AirVelocityY { get; private set; }

    /// <summary>현재 누적 회전량 (도)</summary>
    public double TrickRotation { get; private set; }

    private double _trickAngularVel;
    private double _wipeTimer;

    // 입력 상태 (MainWindow에서 매 프레임 갱신)
    public bool LeanLeft  { get; set; }
    public bool LeanRight { get; set; }
    public bool SpinLeft  { get; set; }
    public bool SpinRight { get; set; }

    public void Update(double dt, WavePhysics wave)
    {
        switch (State)
        {
            case SurferState.OnWave: UpdateOnWave(dt, wave);  break;
            case SurferState.InAir:  UpdateInAir(dt, wave);   break;
            case SurferState.Wiping: UpdateWipeout(dt, wave); break;
        }
    }

    // ─── OnWave ──────────────────────────────────────────────────────────────
    private void UpdateOnWave(double dt, WavePhysics wave)
    {
        ScreenY = wave.SurfaceY(ScreenX);

        // 균형 조작: 좌/우 입력
        double leanDelta = 0;
        if (LeanLeft)  leanDelta -= 2.2 * dt;
        if (LeanRight) leanDelta += 2.2 * dt;

        // 파도 기울기가 자연스럽게 균형에 영향
        double slope = wave.Slope(ScreenX);
        leanDelta += slope * 0.35 * dt;

        Balance = Math.Clamp(Balance + leanDelta, -1.6, 1.6);

        // 자연 복원 (중심으로 서서히 돌아옴)
        Balance -= Balance * 0.6 * dt;

        // 와이프아웃 판정
        if (Math.Abs(Balance) > WipeoutBalance)
        {
            StartWipeout(wave);
            return;
        }

        // 서퍼 각도 = 파도 기울기 + 린 보정
        double slopeAngle = Math.Atan(slope);
        Angle = slopeAngle + Balance * 0.28;

        // 파도 상승 속도가 임계치 초과 → 발사!
        double waveVelY = wave.SurfaceVelocityY(ScreenX);
        if (waveVelY < LaunchVelThreshold)
            Launch(waveVelY);
    }

    private void Launch(double waveVelY)
    {
        State = SurferState.InAir;
        AirVelocityY = waveVelY * 0.55;  // 파도 속도의 55%로 발사
        TrickRotation = 0;
        _trickAngularVel = 0;
    }

    // ─── InAir ───────────────────────────────────────────────────────────────
    private void UpdateInAir(double dt, WavePhysics wave)
    {
        // 중력 적용
        AirVelocityY += Gravity * dt;
        ScreenY += AirVelocityY * dt;

        // 회전 조작
        double targetAngVel = 0;
        if (SpinLeft)  targetAngVel = -MaxAngularVel;
        if (SpinRight) targetAngVel =  MaxAngularVel;

        double velDiff = targetAngVel - _trickAngularVel;
        _trickAngularVel += Math.Sign(velDiff) * Math.Min(Math.Abs(velDiff), AngularAccel * dt);

        // 입력 없을 때 서서히 감쇠
        if (!SpinLeft && !SpinRight)
            _trickAngularVel -= _trickAngularVel * AngularDamping * dt;

        TrickRotation += _trickAngularVel * dt;
        Angle = TrickRotation * Math.PI / 180.0;

        // 착지 판정: 서퍼 Y가 파도 표면 아래로 내려오면
        double waveY = wave.SurfaceY(ScreenX);
        if (ScreenY >= waveY)
            Land(wave);
    }

    /// <summary>착지 → 성공/실패 판정. 반환값: true=클린 랜딩</summary>
    public bool Land(WavePhysics wave)
    {
        ScreenY = wave.SurfaceY(ScreenX);
        AirVelocityY = 0;

        // 회전이 360° 배수(±tolerance)인지 확인
        double full = Math.Abs(TrickRotation);
        bool hasRotation = full >= 150;

        if (!hasRotation)
        {
            // 그냥 착지 (묘기 없음) → 균형 유지로 처리
            Balance *= 0.3;
            State = SurferState.OnWave;
            return false;
        }

        double mod = ((TrickRotation % 360) + 360) % 360;
        double distToFull = Math.Min(mod, 360 - mod); // 0 또는 360에 대한 거리

        if (distToFull <= LandingTolerance)
        {
            // 클린 랜딩
            Balance *= 0.25;
            State = SurferState.OnWave;
            return true;
        }
        else
        {
            // 각도 불일치 → 와이프아웃
            StartWipeout(wave);
            return false;
        }
    }

    // ─── Wipeout ─────────────────────────────────────────────────────────────
    private void StartWipeout(WavePhysics wave)
    {
        State = SurferState.Wiping;
        _wipeTimer = 1.8;
        AirVelocityY = -80; // 와이프아웃 시 살짝 튀어오름
    }

    private void UpdateWipeout(double dt, WavePhysics wave)
    {
        AirVelocityY += Gravity * dt;
        ScreenY = Math.Min(ScreenY + AirVelocityY * dt, wave.SurfaceY(ScreenX) + 30);
        Angle += 8 * dt; // 빙글빙글 돌면서 빠짐

        _wipeTimer -= dt;
        if (_wipeTimer <= 0)
            State = SurferState.Wiped;
    }

    // ─── 공용 ────────────────────────────────────────────────────────────────
    public void ResetOnWave(WavePhysics wave)
    {
        Balance = 0;
        State = SurferState.OnWave;
        AirVelocityY = 0;
        TrickRotation = 0;
        _trickAngularVel = 0;
        _wipeTimer = 0;
        Angle = 0;
        ScreenY = wave.SurfaceY(ScreenX);
    }

    /// <summary>공중에 있는지 여부</summary>
    public bool IsAirborne => State == SurferState.InAir;

    /// <summary>현재 묘기 이름 (공중 회전 기준)</summary>
    public string CurrentAirTrick()
    {
        if (State != SurferState.InAir) return "";
        double full = Math.Abs(TrickRotation);
        if (full < 150) return "";
        int rotations = (int)((full + 30) / 360);
        if (rotations == 0) return "180°";
        return $"{rotations * 360}°";
    }
}
