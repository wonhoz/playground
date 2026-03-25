namespace SandFall.Sim;

/// <summary>셀 하나의 상태</summary>
public struct Cell
{
    public Material Type;
    public byte     Life;      // 불/증기/산 수명 (0이면 소멸)
    public byte     Heat;      // 온도 0-255
    public bool     Updated;   // 이번 프레임에 처리됨
}

/// <summary>Falling Sand 셀룰러 오토마타 그리드</summary>
public sealed class SimGrid
{
    public const int W = 320;
    public const int H = 200;

    private Cell[]   _cells  = new Cell[W * H];
    private uint[]   _pixels = new uint[W * H]; // BGRA32 픽셀 버퍼
    private Random   _rng    = new();

    // 불꽃 색상 팔레트 (뜨거운 → 차가운)
    private static readonly uint[] FireColors =
        [0xFFFFFFAA, 0xFFFFDD00, 0xFFFF8800, 0xFFFF4400, 0xFFCC2200, 0xFF881100];

    public ReadOnlySpan<uint> Pixels => _pixels;

    // ── 인덱스 헬퍼 ─────────────────────────────────────────────────
    private static int Idx(int x, int y) => y * W + x;
    private bool InBounds(int x, int y) => (uint)x < W && (uint)y < H;

    // ── 셀 접근 ─────────────────────────────────────────────────────
    public Material GetType(int x, int y) =>
        InBounds(x, y) ? _cells[Idx(x, y)].Type : Material.Stone; // 경계 밖은 Stone 취급

    private ref Cell At(int x, int y) => ref _cells[Idx(x, y)];

    public void Set(int x, int y, Material m)
    {
        if (!InBounds(x, y)) return;
        ref var c = ref At(x, y);
        c.Type    = m;
        c.Life    = DefaultLife(m);
        c.Heat    = m == Material.Fire ? (byte)200 : (byte)0;
        c.Updated = false;
    }

    public void SetBrush(int cx, int cy, int radius, Material m)
    {
        for (int dy = -radius; dy <= radius; dy++)
        for (int dx = -radius; dx <= radius; dx++)
        {
            if (dx * dx + dy * dy <= radius * radius)
                Set(cx + dx, cy + dy, m);
        }
    }

    private static byte DefaultLife(Material m) => m switch
    {
        Material.Fire  => (byte)(_rng_static.Next(30, 80)),
        Material.Steam => (byte)(_rng_static.Next(40, 100)),
        _              => 0,
    };
    [ThreadStatic] private static Random? _rng_static_field;
    private static Random _rng_static => _rng_static_field ??= new Random();

    // ── 시뮬레이션 스텝 ─────────────────────────────────────────────
    public void Step()
    {
        // Updated 플래그 초기화
        for (int i = 0; i < _cells.Length; i++)
            _cells[i].Updated = false;

        // 아래에서 위로 순회 (중력 시뮬레이션 핵심)
        for (int y = H - 1; y >= 0; y--)
        {
            bool ltr = _rng.Next(2) == 0;
            for (int xi = 0; xi < W; xi++)
            {
                int x = ltr ? xi : W - 1 - xi;
                UpdateCell(x, y);
            }
        }

        // 픽셀 버퍼 갱신
        UpdatePixels();
    }

    private void UpdateCell(int x, int y)
    {
        int idx = Idx(x, y);
        if (_cells[idx].Updated) return;

        switch (_cells[idx].Type)
        {
            case Material.Sand:  UpdateSand(x, y, idx);  break;
            case Material.Water: UpdateWater(x, y, idx); break;
            case Material.Fire:  UpdateFire(x, y, idx);  break;
            case Material.Oil:   UpdateOil(x, y, idx);   break;
            case Material.Steam: UpdateSteam(x, y, idx); break;
            case Material.Seed:  UpdateSeed(x, y, idx);  break;
            case Material.Acid:  UpdateAcid(x, y, idx);  break;
            case Material.Ice:   UpdateIce(x, y, idx);   break;
            case Material.Plant: UpdatePlant(x, y, idx); break;
            case Material.Ash:   UpdateAsh(x, y, idx);   break;
        }
    }

    // ── 이동 헬퍼 ───────────────────────────────────────────────────
    private void Swap(int x1, int y1, int x2, int y2)
    {
        if (!InBounds(x2, y2)) return;
        int i1 = Idx(x1, y1), i2 = Idx(x2, y2);
        (_cells[i1], _cells[i2]) = (_cells[i2], _cells[i1]);
        _cells[i1].Updated = true;
        _cells[i2].Updated = true;
    }

    private bool IsEmpty(int x, int y) =>
        InBounds(x, y) && _cells[Idx(x, y)].Type == Material.Empty;

    private bool IsType(int x, int y, Material m) =>
        InBounds(x, y) && _cells[Idx(x, y)].Type == m;

    private bool IsLiquid(int x, int y)
    {
        if (!InBounds(x, y)) return false;
        var cat = MaterialDef.Category[(int)_cells[Idx(x, y)].Type];
        return cat == MatCategory.Liquid;
    }

    // ── 모래 (Powder) ────────────────────────────────────────────────
    // Sand, Ash: 아래로 낙하, 대각선 미끄러짐, 액체 위에 뜸
    private void UpdateSand(int x, int y, int idx) => UpdatePowder(x, y, idx);
    private void UpdateAsh (int x, int y, int idx) => UpdatePowder(x, y, idx);

    private void UpdatePowder(int x, int y, int idx)
    {
        if (IsEmpty(x, y + 1) || IsLiquid(x, y + 1))
        {
            Swap(x, y, x, y + 1);
            return;
        }
        bool r = _rng.Next(2) == 0;
        int dx1 = r ? -1 : 1, dx2 = r ? 1 : -1;
        if (IsEmpty(x + dx1, y + 1) || IsLiquid(x + dx1, y + 1)) { Swap(x, y, x + dx1, y + 1); return; }
        if (IsEmpty(x + dx2, y + 1) || IsLiquid(x + dx2, y + 1)) { Swap(x, y, x + dx2, y + 1); }
    }

    // ── 물 (Liquid) ───────────────────────────────────────────────────
    private void UpdateWater(int x, int y, int idx)
    {
        // 아래로
        if (IsEmpty(x, y + 1)) { Swap(x, y, x, y + 1); return; }
        // 대각선
        bool r = _rng.Next(2) == 0;
        int dx1 = r ? -1 : 1, dx2 = r ? 1 : -1;
        if (IsEmpty(x + dx1, y + 1)) { Swap(x, y, x + dx1, y + 1); return; }
        if (IsEmpty(x + dx2, y + 1)) { Swap(x, y, x + dx2, y + 1); return; }
        // 수평 흐름
        if (IsEmpty(x + dx1, y)) { Swap(x, y, x + dx1, y); return; }
        if (IsEmpty(x + dx2, y)) { Swap(x, y, x + dx2, y); }
    }

    // ── 기름 (Oil) ────────────────────────────────────────────────────
    private void UpdateOil(int x, int y, int idx)
    {
        // 물보다 가벼워서 물 위로 뜸 (물과 위치 교환)
        if (IsType(x, y - 1, Material.Water)) { Swap(x, y, x, y - 1); return; }
        // 아래로 (물 제외)
        if (IsEmpty(x, y + 1)) { Swap(x, y, x, y + 1); return; }
        bool r = _rng.Next(2) == 0;
        int dx1 = r ? -1 : 1, dx2 = r ? 1 : -1;
        if (IsEmpty(x + dx1, y + 1)) { Swap(x, y, x + dx1, y + 1); return; }
        if (IsEmpty(x + dx2, y + 1)) { Swap(x, y, x + dx2, y + 1); return; }
        // 수평 (물보다 느리게 — 50% 확률)
        if (_rng.Next(2) == 0)
        {
            if (IsEmpty(x + dx1, y)) { Swap(x, y, x + dx1, y); return; }
            if (IsEmpty(x + dx2, y)) { Swap(x, y, x + dx2, y); }
        }
    }

    // ── 불 ───────────────────────────────────────────────────────────
    private void UpdateFire(int x, int y, int idx)
    {
        ref var cell = ref _cells[idx];

        // 수명 감소
        if (--cell.Life == 0)
        {
            cell.Type = Material.Ash;
            cell.Updated = true;
            return;
        }

        // 인접 가연성 물질 점화 (25% 확률)
        if (_rng.Next(4) == 0)
        {
            int nx = x + _rng.Next(-1, 2);
            int ny = y + _rng.Next(-1, 2);
            if (InBounds(nx, ny))
            {
                ref var nb = ref At(nx, ny);
                if (MaterialDef.IsFlammable(nb.Type))
                {
                    nb.Type = Material.Fire;
                    nb.Life = DefaultLife(Material.Fire);
                    nb.Updated = true;
                }
                // 물 → 증기
                else if (nb.Type == Material.Water && _rng.Next(3) == 0)
                {
                    nb.Type = Material.Steam;
                    nb.Life = DefaultLife(Material.Steam);
                    nb.Updated = true;
                }
                // 얼음 → 물
                else if (nb.Type == Material.Ice && _rng.Next(4) == 0)
                {
                    nb.Type = Material.Water;
                    nb.Updated = true;
                }
            }
        }

        // 불은 위로 살짝 이동
        if (_rng.Next(3) == 0 && IsEmpty(x, y - 1))
        {
            Swap(x, y, x, y - 1);
            // 원래 자리에 가끔 재 생성
            if (_rng.Next(3) == 0)
            {
                ref var orig = ref At(x, y);
                orig.Type = Material.Ash;
                orig.Updated = true;
            }
        }
    }

    // ── 증기 ─────────────────────────────────────────────────────────
    private void UpdateSteam(int x, int y, int idx)
    {
        ref var cell = ref _cells[idx];

        // 수명 감소 → 냉각 시 물로 변환
        if (--cell.Life == 0)
        {
            cell.Type = _rng.Next(4) == 0 ? Material.Water : Material.Empty;
            cell.Updated = true;
            return;
        }

        // 위로 이동
        if (IsEmpty(x, y - 1)) { Swap(x, y, x, y - 1); return; }
        bool r = _rng.Next(2) == 0;
        int dx1 = r ? -1 : 1, dx2 = r ? 1 : -1;
        if (IsEmpty(x + dx1, y - 1)) { Swap(x, y, x + dx1, y - 1); return; }
        if (IsEmpty(x + dx2, y - 1)) { Swap(x, y, x + dx2, y - 1); return; }
        if (IsEmpty(x + dx1, y))     { Swap(x, y, x + dx1, y);     return; }
        if (IsEmpty(x + dx2, y))     { Swap(x, y, x + dx2, y); }
    }

    // ── 씨앗 ─────────────────────────────────────────────────────────
    private void UpdateSeed(int x, int y, int idx)
    {
        // 물에 닿으면 식물로 성장 (30% 확률)
        if (_rng.Next(10) == 0)
        {
            for (int dy = -1; dy <= 1; dy++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (IsType(x + dx, y + dy, Material.Water))
                {
                    ref var c = ref At(x, y);
                    c.Type    = Material.Plant;
                    c.Updated = true;
                    return;
                }
            }
        }

        // 아래로 낙하 (Powder처럼)
        UpdatePowder(x, y, idx);
    }

    // ── 식물 ─────────────────────────────────────────────────────────
    private void UpdatePlant(int x, int y, int idx)
    {
        if (_rng.Next(20) != 0) return;  // 천천히 반응

        // 인근 물을 흡수해서 성장
        for (int dy = -1; dy <= 1; dy++)
        for (int dx = -1; dx <= 1; dx++)
        {
            if (dx == 0 && dy == 0) continue;
            if (IsType(x + dx, y + dy, Material.Water))
            {
                // 물 제거 후 빈 곳에 식물 성장
                At(x + dx, y + dy).Type = Material.Empty;
                int gx = x + _rng.Next(-2, 3);
                int gy = y + _rng.Next(-2, 1);  // 주로 위로 성장
                if (IsEmpty(gx, gy))
                {
                    ref var g = ref At(gx, gy);
                    g.Type    = Material.Plant;
                    g.Updated = true;
                }
                return;
            }
        }
    }

    // ── 산 ───────────────────────────────────────────────────────────
    private void UpdateAcid(int x, int y, int idx)
    {
        // 인접 비-산·비-빈 셀 용해
        if (_rng.Next(4) == 0)
        {
            int nx = x + _rng.Next(-1, 2);
            int ny = y + _rng.Next(0, 2);  // 아래 방향 우선
            if (InBounds(nx, ny))
            {
                var nb = At(nx, ny).Type;
                if (nb != Material.Empty && nb != Material.Acid && nb != Material.Water)
                {
                    At(nx, ny).Type    = Material.Empty;
                    _cells[idx].Life   = Math.Max((byte)1, (byte)(_cells[idx].Life - 1));
                    _cells[idx].Updated = true;
                    // 산 자체도 일정 확률로 소비
                    if (_rng.Next(3) == 0) _cells[idx].Type = Material.Empty;
                    return;
                }
            }
        }

        // 물처럼 흐름
        if (IsEmpty(x, y + 1)) { Swap(x, y, x, y + 1); return; }
        bool r = _rng.Next(2) == 0;
        int dx1 = r ? -1 : 1, dx2 = r ? 1 : -1;
        if (IsEmpty(x + dx1, y + 1)) { Swap(x, y, x + dx1, y + 1); return; }
        if (IsEmpty(x + dx2, y + 1)) { Swap(x, y, x + dx2, y + 1); return; }
        if (IsEmpty(x + dx1, y))     { Swap(x, y, x + dx1, y);     return; }
        if (IsEmpty(x + dx2, y))     { Swap(x, y, x + dx2, y); }
    }

    // ── 얼음 ─────────────────────────────────────────────────────────
    private void UpdateIce(int x, int y, int idx)
    {
        if (_rng.Next(20) != 0) return;

        // 인근 물을 얼림 (10% 확률)
        if (_rng.Next(10) == 0)
        {
            int nx = x + _rng.Next(-1, 2);
            int ny = y + _rng.Next(-1, 2);
            if (IsType(nx, ny, Material.Water))
            {
                At(nx, ny).Type = Material.Ice;
            }
        }
    }

    // ── 픽셀 렌더링 ──────────────────────────────────────────────────
    private void UpdatePixels()
    {
        for (int i = 0; i < _cells.Length; i++)
        {
            var mat = _cells[i].Type;
            uint color = MaterialDef.BaseColor[(int)mat];

            // 물질별 색상 변화
            if (mat == Material.Fire)
            {
                // 수명에 따라 색상 변화 (뜨거울수록 밝음)
                int life = _cells[i].Life;
                int ci = Math.Clamp(FireColors.Length - 1 - (life * FireColors.Length / 80), 0, FireColors.Length - 1);
                color = FireColors[ci];
                // 약간의 노이즈
                if (_rng.Next(3) == 0)
                    ci = Math.Clamp(ci + _rng.Next(-1, 2), 0, FireColors.Length - 1);
                color = FireColors[ci];
            }
            else if (mat == Material.Water)
            {
                // 파란색 약간 변화
                color = (_rng.Next(3) == 0) ? 0xFF4070D8u : 0xFF3468C8u;
            }
            else if (mat == Material.Steam)
            {
                // Life에 따라 배경색(#0D1117)으로 CPU 블렌딩 → 증기 페이드 시각화
                int a = Math.Clamp(_cells[i].Life * 3, 0, 255);
                int r = (0x9A * a + 0x0D * (255 - a)) / 255;
                int g = (0xAA * a + 0x11 * (255 - a)) / 255;
                int b = (0xC0 * a + 0x17 * (255 - a)) / 255;
                color = (uint)((r << 16) | (g << 8) | b);
            }
            else if (mat == Material.Sand)
            {
                // 모래 약간 색상 변화
                color = (_rng.Next(4) == 0) ? 0xFFDDC060u : 0xFFE8C878u;
            }
            else if (mat == Material.Plant)
            {
                // 식물 색상 변화
                color = (_rng.Next(3) == 0) ? 0xFF18902Au : 0xFF22A030u;
            }

            _pixels[i] = color;
        }
    }

    /// <summary>그리드 전체 초기화</summary>
    public void Clear()
    {
        Array.Clear(_cells, 0, _cells.Length);
        Array.Clear(_pixels, 0, _pixels.Length);
    }
}
