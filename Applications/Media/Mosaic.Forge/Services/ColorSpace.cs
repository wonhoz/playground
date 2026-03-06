namespace Mosaic.Forge.Services;

/// <summary>sRGB ↔ CIE Lab 색 공간 변환</summary>
static class ColorSpace
{
    // D65 표준 광원 기준
    const double Xn = 0.95047, Yn = 1.00000, Zn = 1.08883;

    public static (double L, double A, double B) RgbToLab(byte r, byte g, byte b)
    {
        double rL = ToLinear(r / 255.0);
        double gL = ToLinear(g / 255.0);
        double bL = ToLinear(b / 255.0);

        double X = 0.4124564 * rL + 0.3575761 * gL + 0.1804375 * bL;
        double Y = 0.2126729 * rL + 0.7151522 * gL + 0.0721750 * bL;
        double Z = 0.0193339 * rL + 0.1191920 * gL + 0.9503041 * bL;

        double fx = F(X / Xn);
        double fy = F(Y / Yn);
        double fz = F(Z / Zn);

        return (116.0 * fy - 16.0, 500.0 * (fx - fy), 200.0 * (fy - fz));
    }

    static double ToLinear(double c)
        => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);

    static double F(double t)
    {
        const double delta = 6.0 / 29.0;
        return t > delta * delta * delta
            ? Math.Pow(t, 1.0 / 3.0)
            : t / (3.0 * delta * delta) + 4.0 / 29.0;
    }

    /// Lab 공간 제곱 유클리드 거리 (비교용, sqrt 불필요)
    public static double DistanceSq(double L1, double A1, double B1,
                                    double L2, double A2, double B2)
    {
        double dL = L1 - L2, dA = A1 - A2, dB = B1 - B2;
        return dL * dL + dA * dA + dB * dB;
    }
}
