namespace Color.Grade.Services;

/// <summary>.cube 형식 3D LUT 파서</summary>
public class Lut3D
{
    public int      Size { get; }
    public float[]  Data { get; }  // [idx*3+0]=R, [idx*3+1]=G, [idx*3+2]=B
    // 인덱스: r + g*Size + b*Size*Size

    public Lut3D(int size, float[] data)
    {
        Size = size;
        Data = data;
    }

    /// <summary>삼선형 보간으로 LUT 적용</summary>
    public (float R, float G, float B) Apply(float r, float g, float b)
    {
        float rf = Math.Clamp(r, 0f, 1f) * (Size - 1);
        float gf = Math.Clamp(g, 0f, 1f) * (Size - 1);
        float bf = Math.Clamp(b, 0f, 1f) * (Size - 1);

        int ri = (int)rf, gi = (int)gf, bi = (int)bf;
        float rt = rf - ri, gt = gf - gi, bt = bf - bi;

        ri = Math.Min(ri, Size - 2);
        gi = Math.Min(gi, Size - 2);
        bi = Math.Min(bi, Size - 2);

        // 8 코너 샘플
        var c000 = Get(ri,   gi,   bi);
        var c100 = Get(ri+1, gi,   bi);
        var c010 = Get(ri,   gi+1, bi);
        var c110 = Get(ri+1, gi+1, bi);
        var c001 = Get(ri,   gi,   bi+1);
        var c101 = Get(ri+1, gi,   bi+1);
        var c011 = Get(ri,   gi+1, bi+1);
        var c111 = Get(ri+1, gi+1, bi+1);

        float Lerp3(int ch)
        {
            float v000 = c000[ch], v100 = c100[ch], v010 = c010[ch], v110 = c110[ch];
            float v001 = c001[ch], v101 = c101[ch], v011 = c011[ch], v111 = c111[ch];
            float i00 = v000 + (v100 - v000) * rt;
            float i10 = v010 + (v110 - v010) * rt;
            float i01 = v001 + (v101 - v001) * rt;
            float i11 = v011 + (v111 - v011) * rt;
            float j0  = i00  + (i10  - i00)  * gt;
            float j1  = i01  + (i11  - i01)  * gt;
            return j0 + (j1 - j0) * bt;
        }

        return (Lerp3(0), Lerp3(1), Lerp3(2));
    }

    float[] Get(int r, int g, int b)
    {
        int idx = (r + g * Size + b * Size * Size) * 3;
        return [Data[idx], Data[idx + 1], Data[idx + 2]];
    }
}

public static class LutParser
{
    public static Lut3D? Parse(string path)
    {
        int size = 0;
        var entries = new List<float[]>();
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith('#') || line.Length == 0) continue;
            if (line.StartsWith("LUT_3D_SIZE", StringComparison.OrdinalIgnoreCase))
            {
                int.TryParse(line.Split(' ')[^1], out size);
                continue;
            }
            if (line.StartsWith("TITLE", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("DOMAIN", StringComparison.OrdinalIgnoreCase)) continue;
            var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length >= 3
                && float.TryParse(parts[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float rv)
                && float.TryParse(parts[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float gv)
                && float.TryParse(parts[2], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float bv))
            {
                entries.Add([rv, gv, bv]);
            }
        }
        if (size == 0 || entries.Count != size * size * size) return null;
        var data = new float[entries.Count * 3];
        for (int i = 0; i < entries.Count; i++)
        {
            data[i * 3]     = entries[i][0];
            data[i * 3 + 1] = entries[i][1];
            data[i * 3 + 2] = entries[i][2];
        }
        return new Lut3D(size, data);
    }
}
