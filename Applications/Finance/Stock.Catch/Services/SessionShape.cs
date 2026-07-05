namespace Stock.Catch.Services;

/// <summary>
/// 세션(프리·애프터) 등락율 시계열의 궤적 형태를 분류한다 — 방향(상승/하락)과 곡률/전환으로
/// 선형·로그(감속)·제곱(가속)·V·역V·Z(등락 반복)·횡보를 구분해 위트 있는 한 줄 태그를 만든다.
/// </summary>
public static class SessionShape
{
    /// <summary>시계열(시간순 등락율 %) → (이모지, 형태명). 표본 부족/미세 변동은 횡보.</summary>
    public static (string Icon, string Name) Classify(IReadOnlyList<double> s)
    {
        int n = s.Count;
        if (n < 5) return ("➡️", "데이터 부족");

        double min = s[0], max = s[0]; int minIdx = 0, maxIdx = 0;
        for (int i = 0; i < n; i++)
        {
            if (s[i] < min) { min = s[i]; minIdx = i; }
            if (s[i] > max) { max = s[i]; maxIdx = i; }
        }
        double range = max - min;
        if (range < 0.4) return ("➡️", "횡보(잔잔)");   // 세션 내 진폭이 작으면 방향성 없음

        double dir = s[n - 1] - s[0];
        double mMin = minIdx / (double)(n - 1), mMax = maxIdx / (double)(n - 1);

        // V자: 최저가 구간 중앙(0.15~0.85)이고 양끝이 최저보다 충분히 높음.
        if (mMin is > 0.15 and < 0.85 && s[0] - min > range * 0.35 && s[n - 1] - min > range * 0.35)
            return ("✅", "V자 반등(저점 딛고 상승)");
        // 역V(Λ): 최고가 중앙이고 양끝이 최고보다 충분히 낮음.
        if (mMax is > 0.15 and < 0.85 && max - s[0] > range * 0.35 && max - s[n - 1] > range * 0.35)
            return ("⚠️", "역V(고점 찍고 하락)");

        // Z(등락 반복): 유의미(range의 30% 이상) 방향 전환이 3회 이상.
        int turns = CountTurns(s, range * 0.30);
        if (turns >= 3) return ("〰️", "등락 반복(Z·방향성 약함)");

        // 단조 추세 — 선형 대비 중간 편차(곡률)로 선형/로그(감속)/제곱(가속) 구분.
        double dev = 0;
        for (int i = 0; i < n; i++)
        {
            double lin = s[0] + (s[n - 1] - s[0]) * i / (n - 1);   // 시작→끝 직선
            dev += s[i] - lin;
        }
        dev /= n;                       // 양수=실측이 직선 위(오목), 음수=아래(볼록)
        double curv = dev / range;      // 정규화 곡률

        bool up = dir >= 0;
        if (Math.Abs(curv) < 0.08)
            return (up ? "📈" : "📉", up ? "선형 상승(꾸준)" : "선형 하락(꾸준)");
        if (up)
            return curv > 0 ? ("🌙", "로그형 상승(초반 급→감속)") : ("🚀", "제곱형 상승(후반 가속)");
        return curv < 0 ? ("🧊", "로그형 하락(초반 급락→감속)") : ("🔻", "제곱형 하락(후반 가속)");
    }

    /// <summary>진폭 임계(minSwing) 이상의 방향 전환 횟수 — 지그재그(Z) 판별용.</summary>
    private static int CountTurns(IReadOnlyList<double> s, double minSwing)
    {
        int turns = 0, dir = 0;   // 현재 진행 방향(+1/−1)
        double pivot = s[0];
        foreach (var v in s)
        {
            if (dir >= 0 && v - pivot <= -minSwing) { if (dir > 0) turns++; dir = -1; pivot = v; }
            else if (dir <= 0 && v - pivot >= minSwing) { if (dir < 0) turns++; dir = 1; pivot = v; }
            else { if (dir > 0) pivot = Math.Max(pivot, v); else if (dir < 0) pivot = Math.Min(pivot, v); }
        }
        return turns;
    }
}
