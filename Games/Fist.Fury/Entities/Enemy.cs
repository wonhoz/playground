using System.Windows.Media;

namespace FistFury.Entities;

public enum EnemyKind { Thug, Brute, Ninja, Boss }

public sealed class Enemy : Fighter
{
    private readonly Random _rng;
    private readonly Fighter _target;
    private double _aiTimer;
    private double _aiDecisionInterval;

    public EnemyKind Kind { get; }
    public int ScoreValue { get; }

    public Enemy(EnemyKind kind, double startX, Fighter target, Random rng)
    {
        _rng = rng;
        _target = target;
        Kind = kind;

        X = startX;
        Y = GroundY;
        Facing = startX > target.X ? FacingDir.Left : FacingDir.Right;

        switch (kind)
        {
            case EnemyKind.Thug:
                Name = "THUG";
                MaxHp = 40; Hp = 40;
                MoveSpeed = 140;
                ScoreValue = 100;
                _aiDecisionInterval = 0.8;
                CreateVisual(Color.FromRgb(0xE7, 0x4C, 0x3C), Color.FromRgb(0xDD, 0xAA, 0x77));
                break;
            case EnemyKind.Brute:
                Name = "BRUTE";
                MaxHp = 80; Hp = 80;
                MoveSpeed = 90;
                Width = 60; Height = 90;
                ScoreValue = 250;
                _aiDecisionInterval = 1.2;
                CreateVisual(Color.FromRgb(0x8E, 0x44, 0xAD), Color.FromRgb(0xCC, 0x99, 0x77));
                break;
            case EnemyKind.Ninja:
                Name = "NINJA";
                MaxHp = 30; Hp = 30;
                MoveSpeed = 280;
                Width = 44; Height = 74;
                ScoreValue = 200;
                _aiDecisionInterval = 0.5;
                CreateVisual(Color.FromRgb(0x2C, 0x3E, 0x50), Color.FromRgb(0x44, 0x44, 0x44));
                break;
            case EnemyKind.Boss:
                Name = "BOSS";
                MaxHp = 200; Hp = 200;
                MoveSpeed = 120;
                Width = 70; Height = 95;
                ScoreValue = 1000;
                _aiDecisionInterval = 0.6;
                CreateVisual(Color.FromRgb(0xC0, 0x39, 0x2B), Color.FromRgb(0xFF, 0xDD, 0x44));
                break;
        }

        _aiTimer = _rng.NextDouble() * _aiDecisionInterval;
    }

    public override void Update(double dt)
    {
        if (!IsAlive) { base.Update(dt); return; }
        if (State == FighterState.Hit) { base.Update(dt); return; }

        _aiTimer -= dt;
        if (_aiTimer <= 0)
        {
            _aiTimer = _aiDecisionInterval + _rng.NextDouble() * 0.3;
            DecideAction();
        }

        base.Update(dt);
    }

    private void DecideAction()
    {
        double dist = Math.Abs(X - _target.X);
        Facing = _target.X > X ? FacingDir.Right : FacingDir.Left;

        double attackRange = Kind == EnemyKind.Boss ? 70 : 55;

        if (dist < attackRange)
        {
            // 근접 → 공격
            double roll = _rng.NextDouble();
            if (Kind == EnemyKind.Boss && roll < 0.3)
                DoSpecial();
            else if (roll < 0.5)
                DoPunch();
            else
                DoKick();
        }
        else if (dist < 300)
        {
            // 접근
            VelocityX = Facing == FacingDir.Right ? MoveSpeed : -MoveSpeed;
            State = FighterState.Walk;

            // 닌자는 가끔 점프
            if (Kind == EnemyKind.Ninja && _rng.NextDouble() < 0.3)
                DoJump();
        }
        else
        {
            // 너무 멀면 빠르게 접근
            VelocityX = Facing == FacingDir.Right ? MoveSpeed * 1.3 : -MoveSpeed * 1.3;
            State = FighterState.Walk;
        }
    }
}
