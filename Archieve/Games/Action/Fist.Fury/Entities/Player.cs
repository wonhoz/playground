using System.Windows.Media;
using FistFury.Engine;

namespace FistFury.Entities;

public sealed class Player : Fighter
{
    private readonly KeyInput _input;
    private bool _punchReleased = true;
    private bool _kickReleased = true;
    private bool _specialReleased = true;
    private bool _jumpReleased = true;

    public int Score { get; set; }

    public Player(KeyInput input, double startX)
    {
        _input = input;
        Name = "PLAYER";
        X = startX;
        Y = GroundY;
        MaxHp = 150;
        Hp = 150;
        MoveSpeed = 260;
        CreateVisual(
            Color.FromRgb(0x3A, 0x86, 0xFF),  // 파란 몸체
            Color.FromRgb(0xFF, 0xCC, 0x88));  // 살색 머리
    }

    public override void Update(double dt)
    {
        if (!IsAlive) { base.Update(dt); return; }

        // 히트 중엔 입력 무시
        if (State is not FighterState.Hit)
        {
            // 이동
            if (_input.Left && State is FighterState.Idle or FighterState.Walk)
            {
                VelocityX = -MoveSpeed;
                Facing = FacingDir.Left;
                if (IsOnGround) State = FighterState.Walk;
            }
            else if (_input.Right && State is FighterState.Idle or FighterState.Walk)
            {
                VelocityX = MoveSpeed;
                Facing = FacingDir.Right;
                if (IsOnGround) State = FighterState.Walk;
            }
            else if (IsOnGround && State == FighterState.Walk)
            {
                State = FighterState.Idle;
            }

            // 점프 (토글)
            if (_input.Jump && _jumpReleased && IsOnGround)
            {
                DoJump();
                _jumpReleased = false;
            }
            if (!_input.Jump) _jumpReleased = true;

            // 공격 (토글)
            if (_input.Special && _specialReleased)
            {
                DoSpecial();
                _specialReleased = false;
            }
            else if (_input.Kick && _kickReleased)
            {
                DoKick();
                _kickReleased = false;
            }
            else if (_input.Punch && _punchReleased)
            {
                DoPunch();
                _punchReleased = false;
            }

            if (!_input.Punch) _punchReleased = true;
            if (!_input.Kick) _kickReleased = true;
            if (!_input.Special) _specialReleased = true;
        }

        base.Update(dt);
    }

    public void FullReset(double startX)
    {
        X = startX;
        Y = GroundY;
        Hp = MaxHp;
        Score = 0;
        ComboCount = 0;
        State = FighterState.Idle;
        VelocityX = 0;
        VelocityY = 0;
        Visual.Opacity = 1.0;
    }
}
