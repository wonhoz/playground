using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace FistFury.Entities;

public enum FighterState { Idle, Walk, Jump, Punch, Kick, Special, Hit, Dead }
public enum FacingDir { Right, Left }

public class Fighter
{
    // 상수
    protected const double Gravity = 1800;
    protected const double JumpForce = -620;
    protected const double GroundY = 420; // 바닥 Y 좌표

    // 위치/물리
    public double X { get; set; }
    public double Y { get; set; }
    public double VelocityX { get; set; }
    public double VelocityY { get; set; }
    public double Width { get; set; } = 50;
    public double Height { get; set; } = 80;
    public bool IsOnGround => Y >= GroundY;
    public bool IsAlive => Hp > 0;
    public FacingDir Facing { get; set; } = FacingDir.Right;

    // 스탯
    public string Name { get; set; } = "";
    public int MaxHp { get; set; } = 100;
    public int Hp { get; set; } = 100;
    public double MoveSpeed { get; set; } = 220;

    // 상태
    public FighterState State { get; set; } = FighterState.Idle;
    protected double StateTimer;
    protected double AttackCooldown;
    protected double HitStunTimer;
    protected double InvincibleTimer;

    // 비주얼
    public Canvas Visual { get; protected set; } = null!;
    protected Rectangle BodyRect = null!;
    protected Rectangle HeadRect = null!;
    protected Rectangle ArmRect = null!;
    protected Rectangle LegRect = null!;

    // 공격 히트박스 (활성 시)
    public Rect? ActiveHitbox { get; protected set; }
    public int ActiveDamage { get; protected set; }
    public double ActiveKnockback { get; protected set; }
    private bool _hitRegistered; // 한 공격당 1번만 히트

    // 콤보
    public int ComboCount { get; set; }
    public double ComboTimer { get; set; }

    public Rect Bounds => new(X, Y, Width, Height);

    public virtual void CreateVisual(Color bodyColor, Color headColor)
    {
        Visual = new Canvas { Width = Width, Height = Height };

        // 다리
        LegRect = new Rectangle
        {
            Width = 14, Height = 28,
            Fill = new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x66)),
            RadiusX = 3, RadiusY = 3
        };
        Canvas.SetLeft(LegRect, Width / 2 - 7);
        Canvas.SetTop(LegRect, Height - 28);

        // 몸체
        BodyRect = new Rectangle
        {
            Width = 30, Height = 36,
            Fill = new SolidColorBrush(bodyColor),
            RadiusX = 4, RadiusY = 4
        };
        Canvas.SetLeft(BodyRect, Width / 2 - 15);
        Canvas.SetTop(BodyRect, Height - 60);

        // 팔
        ArmRect = new Rectangle
        {
            Width = 10, Height = 24,
            Fill = new SolidColorBrush(headColor),
            RadiusX = 3, RadiusY = 3
        };
        Canvas.SetLeft(ArmRect, Width / 2 + 15);
        Canvas.SetTop(ArmRect, Height - 56);

        // 머리
        HeadRect = new Rectangle
        {
            Width = 22, Height = 22,
            Fill = new SolidColorBrush(headColor),
            RadiusX = 11, RadiusY = 11
        };
        Canvas.SetLeft(HeadRect, Width / 2 - 11);
        Canvas.SetTop(HeadRect, 0);

        Visual.Children.Add(LegRect);
        Visual.Children.Add(BodyRect);
        Visual.Children.Add(ArmRect);
        Visual.Children.Add(HeadRect);
    }

    public virtual void Update(double dt)
    {
        // 중력
        if (!IsOnGround)
        {
            VelocityY += Gravity * dt;
            Y += VelocityY * dt;
            if (Y >= GroundY)
            {
                Y = GroundY;
                VelocityY = 0;
            }
        }

        // X 이동
        X += VelocityX * dt;
        X = Math.Clamp(X, 0, 750 - Width);

        // 상태 타이머
        if (StateTimer > 0)
        {
            StateTimer -= dt;
            if (StateTimer <= 0)
            {
                ActiveHitbox = null;
                _hitRegistered = false;
                if (State is FighterState.Punch or FighterState.Kick or FighterState.Special or FighterState.Hit)
                    State = FighterState.Idle;
            }
        }

        if (AttackCooldown > 0) AttackCooldown -= dt;
        if (HitStunTimer > 0) HitStunTimer -= dt;
        if (InvincibleTimer > 0) InvincibleTimer -= dt;

        // 콤보 타이머
        if (ComboTimer > 0)
        {
            ComboTimer -= dt;
            if (ComboTimer <= 0) ComboCount = 0;
        }

        UpdateVisual();
    }

    protected void StartAttack(FighterState attackState, double duration, int damage, double knockback, double hitboxOffsetX, double hitboxW, double hitboxH)
    {
        if (AttackCooldown > 0 || State is FighterState.Hit) return;
        State = attackState;
        StateTimer = duration;
        AttackCooldown = duration + 0.08;
        ActiveDamage = damage;
        ActiveKnockback = knockback;
        _hitRegistered = false;

        double hx = Facing == FacingDir.Right ? X + Width + hitboxOffsetX : X - hitboxW - hitboxOffsetX;
        ActiveHitbox = new Rect(hx, Y + Height / 2 - hitboxH / 2, hitboxW, hitboxH);
    }

    public void DoPunch()
    {
        if (!IsOnGround || State is FighterState.Hit) return;
        StartAttack(FighterState.Punch, 0.2, 8, 120, -5, 30, 20);
    }

    public void DoKick()
    {
        if (!IsOnGround || State is FighterState.Hit) return;
        StartAttack(FighterState.Kick, 0.3, 12, 180, -5, 35, 24);
    }

    public void DoSpecial()
    {
        if (!IsOnGround || State is FighterState.Hit) return;
        StartAttack(FighterState.Special, 0.5, 25, 300, -5, 45, 30);
    }

    public void DoJump()
    {
        if (!IsOnGround || State is FighterState.Hit) return;
        VelocityY = JumpForce;
        Y -= 1; // 바닥 탈출
        State = FighterState.Jump;
    }

    public bool TryHit(Fighter target)
    {
        if (ActiveHitbox is null || _hitRegistered || !target.IsAlive) return false;
        if (target.InvincibleTimer > 0) return false;

        var hitbox = ActiveHitbox.Value;
        if (!hitbox.IntersectsWith(target.Bounds)) return false;

        _hitRegistered = true;
        target.ReceiveHit(ActiveDamage, Facing == FacingDir.Right ? ActiveKnockback : -ActiveKnockback);
        ComboCount++;
        ComboTimer = 2.0;
        return true;
    }

    public void ReceiveHit(int damage, double knockback)
    {
        Hp = Math.Max(0, Hp - damage);
        State = FighterState.Hit;
        StateTimer = 0.35;
        HitStunTimer = 0.35;
        VelocityX = knockback;
        InvincibleTimer = 0.4;

        if (Hp <= 0) State = FighterState.Dead;
    }

    protected virtual void UpdateVisual()
    {
        if (Visual is null) return;

        // 좌우 반전
        double scaleX = Facing == FacingDir.Left ? -1 : 1;
        Visual.RenderTransform = new ScaleTransform(scaleX, 1, Width / 2, Height / 2);

        // 상태별 팔/다리 애니메이션
        switch (State)
        {
            case FighterState.Punch:
                Canvas.SetLeft(ArmRect, Width / 2 + 15);
                Canvas.SetTop(ArmRect, Height - 58);
                ArmRect.Width = 26;
                ArmRect.Height = 10;
                break;
            case FighterState.Kick:
                Canvas.SetLeft(LegRect, Width / 2 + 8);
                Canvas.SetTop(LegRect, Height - 20);
                LegRect.Width = 28;
                LegRect.Height = 10;
                break;
            case FighterState.Special:
                Canvas.SetLeft(ArmRect, Width / 2 + 15);
                Canvas.SetTop(ArmRect, Height - 55);
                ArmRect.Width = 32;
                ArmRect.Height = 12;
                ArmRect.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0xD7, 0x00));
                break;
            case FighterState.Hit:
                Visual.Opacity = 0.7;
                break;
            default:
                // 리셋
                Canvas.SetLeft(ArmRect, Width / 2 + 15);
                Canvas.SetTop(ArmRect, Height - 56);
                ArmRect.Width = 10;
                ArmRect.Height = 24;
                Canvas.SetLeft(LegRect, Width / 2 - 7);
                Canvas.SetTop(LegRect, Height - 28);
                LegRect.Width = 14;
                LegRect.Height = 28;
                Visual.Opacity = 1.0;
                break;
        }

        // 감속
        if (IsOnGround && State is not (FighterState.Hit))
            VelocityX *= 0.85;
    }

    public void SyncPosition()
    {
        Canvas.SetLeft(Visual, X);
        Canvas.SetTop(Visual, Y);
    }
}
