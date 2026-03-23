using StackCrash.Models;

namespace StackCrash.Game;

/// <summary>물리 바디 + WPF 비주얼을 묶는 게임 블록</summary>
public class GameBlock
{
    // ── 물리 ─────────────────────────────────────────────────────────
    public Body   Body     { get; init; } = null!;
    public float  PhysW    { get; init; }   // 물리 너비 (미터)
    public float  PhysH    { get; init; }   // 물리 높이 (미터)

    // ── 게임 상태 ────────────────────────────────────────────────────
    public BlockMaterial Material  { get; init; }
    public int            Hp       { get; set;  }
    public bool           IsRemoved { get; set; }
    public bool           IsExploding { get; set; }

    // ── WPF 비주얼 ──────────────────────────────────────────────────
    public Grid Visual { get; init; } = null!;   // 외부 컨테이너 (RenderTransform 적용)

    // ── 좌표 변환 상수 ───────────────────────────────────────────────
    private const double PPM = 60.0;

    /// <summary>물리 바디 위치에 맞게 WPF 비주얼을 동기화합니다.</summary>
    public void SyncVisual(double groundScreenY, double canvasCenterX)
    {
        if (IsRemoved) return;

        var pos = Body.Position;
        double screenX = pos.X * PPM + canvasCenterX;
        double screenY = groundScreenY - pos.Y * PPM;

        double w = PhysW * PPM;
        double h = PhysH * PPM;

        Canvas.SetLeft(Visual, screenX - w / 2.0);
        Canvas.SetTop (Visual, screenY - h / 2.0);

        // 물리 CCW + Y축 반전 → WPF CW (부호 그대로)
        double deg = Body.Rotation * (180.0 / Math.PI);
        var rt = (RotateTransform)Visual.RenderTransform;
        rt.Angle = deg;
    }

    /// <summary>충격 HP 감소. 0이 되면 true 반환 (파괴).</summary>
    public bool TakeDamage(int dmg)
    {
        Hp -= dmg;
        UpdateVisualHp();
        return Hp <= 0;
    }

    private void UpdateVisualHp()
    {
        // HP에 따라 균열 오버레이 투명도 변경
        if (Visual.Children.Count > 1 && Visual.Children[1] is Border crackOverlay)
        {
            var def = Materials.Get(Material);
            double ratio = Math.Max(0, (double)Hp / def.MaxHp);
            crackOverlay.Opacity = 1.0 - ratio;   // HP 낮을수록 균열 진하게
        }
    }
}
