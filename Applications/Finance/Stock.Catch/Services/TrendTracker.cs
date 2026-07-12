namespace Stock.Catch.Services;

/// <summary>추세 펄스 1건 — 지속 마일스톤 도달 또는 추세 전환.</summary>
public readonly record struct TrendPulse(
    bool IsFlip,          // true=전환(상승↔하락), false=지속 마일스톤 도달
    bool Up,              // 현재(전환 후) 방향이 상승인지
    int Milestone,        // 지속 마일스톤(분) — IsFlip이면 0
    double RunMinutes,    // 현재 런 지속(분)
    double RunPct,        // 현재 런 등락(%)
    double PrevRunMinutes,// 전환 시 직전 런 지속(분)
    double PrevRunPct,    // 전환 시 직전 런 등락(%)
    string Horizons);     // 다중 호라이즌 요약("3분↑ 5분↑ 10분↓ …")

/// <summary>
/// 종목 1개의 가격 시계열을 받아 <b>연속 상승/하락 지속 시간</b>과 <b>추세 전환</b>을 감지한다(지그재그 피벗).
/// 되돌림 임계(reversalPct)만큼 극점에서 되돌리면 전환으로 보고, 그 전까지는 같은 방향 런이 이어진다.
/// 매 갱신마다 지정한 지속 마일스톤(3/5/10/30/60분)을 새로 넘으면 지속 펄스를, 전환되면 전환 펄스를 낸다.
/// 폴링 주기(수십 초~1분)로 급여되며, 다중 호라이즌 방향은 보관된 시계열에서 계산한다.
/// </summary>
public sealed class TrendTracker(double reversalPct)
{
    private readonly List<(DateTime t, decimal p)> _hist = new();
    private readonly double _thr = Math.Max(0.05, reversalPct) / 100.0;
    private int _dir;                      // +1 상승 · -1 하락 · 0 방향 미확정
    private DateTime _pivotT; private decimal _pivotP;   // 런 시작(직전 전환점)
    private DateTime _extT; private decimal _extP;       // 현재 런의 극점(상승=고점·하락=저점)
    private readonly HashSet<int> _fired = new();        // 이번 런에서 이미 발화한 마일스톤

    /// <summary>새 가격 표본을 반영하고 발생한 펄스(0~2건)를 반환한다.</summary>
    public List<TrendPulse> Update(DateTime t, decimal p, IReadOnlyList<int> milestones, IReadOnlyList<int> horizons)
    {
        var pulses = new List<TrendPulse>();
        if (p <= 0) return pulses;
        _hist.Add((t, p));
        var cut = t.AddMinutes(-Math.Max(70, horizons.DefaultIfEmpty(60).Max() + 5));
        while (_hist.Count > 0 && _hist[0].t < cut) _hist.RemoveAt(0);

        if (_dir == 0)
        {
            // 방향 미확정: 피벗=저점, 극점=고점을 함께 추적하다 임계 돌파 시 방향 확정.
            if (_pivotP == 0) { _pivotT = t; _pivotP = p; _extT = t; _extP = p; return pulses; }
            if (p > _extP) { _extP = p; _extT = t; }
            if (p < _pivotP) { _pivotP = p; _pivotT = t; }
            if (p >= _pivotP * (decimal)(1 + _thr)) { _dir = 1; _extP = p; _extT = t; }        // 저점서 상승 확정
            else if (p <= _extP * (decimal)(1 - _thr)) { _dir = -1; _pivotT = _extT; _pivotP = _extP; _extP = p; _extT = t; }  // 고점서 하락 확정
        }
        else if (_dir == 1)
        {
            if (p > _extP) { _extP = p; _extT = t; }
            else if (p <= _extP * (decimal)(1 - _thr))
                pulses.Add(Flip(t, p, up: false, horizons));   // 상승→하락 전환
        }
        else // _dir == -1
        {
            if (p < _extP) { _extP = p; _extT = t; }
            else if (p >= _extP * (decimal)(1 + _thr))
                pulses.Add(Flip(t, p, up: true, horizons));    // 하락→상승 전환
        }

        // 지속 마일스톤(전환이 없을 때만 · 방향 확정 상태)
        if (_dir != 0 && pulses.Count == 0)
        {
            double runMin = (t - _pivotT).TotalMinutes;
            double runPct = _pivotP > 0 ? (double)(p / _pivotP - 1) * 100 : 0;
            foreach (var m in milestones.Where(m => m > 0).OrderBy(m => m))
                if (runMin >= m && _fired.Add(m))
                {
                    pulses.Add(new TrendPulse(false, _dir == 1, m, runMin, runPct, 0, 0, Horizons(t, horizons)));
                    break;   // 한 번에 하나만
                }
        }
        return pulses;
    }

    private TrendPulse Flip(DateTime t, decimal p, bool up, IReadOnlyList<int> horizons)
    {
        double prevMin = (_extT - _pivotT).TotalMinutes;
        double prevPct = _pivotP > 0 ? (double)(_extP / _pivotP - 1) * 100 : 0;
        _pivotT = _extT; _pivotP = _extP;   // 새 런은 직전 극점(전환점)에서 시작
        _extP = p; _extT = t; _dir = up ? 1 : -1;
        _fired.Clear();
        double runMin = (t - _pivotT).TotalMinutes;
        double runPct = _pivotP > 0 ? (double)(p / _pivotP - 1) * 100 : 0;
        return new TrendPulse(true, up, 0, runMin, runPct, prevMin, prevPct, Horizons(t, horizons));
    }

    /// <summary>다중 호라이즌 방향 요약: 각 H분 전 표본 대비 현재가 방향(↑/↓/·).</summary>
    private string Horizons(DateTime now, IReadOnlyList<int> horizons)
    {
        if (_hist.Count == 0) return "";
        decimal cur = _hist[^1].p;
        var parts = new List<string>(horizons.Count);
        foreach (var h in horizons.Where(h => h > 0))
        {
            var target = now.AddMinutes(-h);
            decimal? past = null;
            for (int i = _hist.Count - 1; i >= 0; i--) { if (_hist[i].t <= target) { past = _hist[i].p; break; } }
            if (past is not { } pp || pp <= 0) { parts.Add($"{h}분·"); continue; }
            double chg = (double)(cur / pp - 1) * 100;
            string arrow = chg >= 0.1 ? "↑" : chg <= -0.1 ? "↓" : "·";
            parts.Add($"{h}분{arrow}");
        }
        return string.Join(" ", parts);
    }
}
