namespace CharArt.Services;

/// <summary>
/// 밝기 행렬 → 텍스트 아트 문자열 변환
/// </summary>
public static class TextArtGenerator
{
    /// <summary>
    /// orderedChars: 밀한 문자(인덱스 0) → 밝은/공백 문자(인덱스 last)
    /// </summary>
    public static string Generate(float[,] brightness, char[] orderedChars, bool invert)
    {
        if (orderedChars.Length == 0) return string.Empty;

        int rows = brightness.GetLength(0);
        int cols = brightness.GetLength(1);

        var sb = new StringBuilder(rows * (cols + Environment.NewLine.Length));

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                float b = invert ? 1f - brightness[r, c] : brightness[r, c];
                // b=0(검정) → 마지막 문자(공백), b=1(흰색) → 인덱스 0(가장 밀한 문자)
                // 반전 없을 때: 어두울수록 밀한 문자
                float mapped = invert ? b : (1f - b);
                int idx = Math.Clamp((int)(mapped * orderedChars.Length), 0, orderedChars.Length - 1);
                sb.Append(orderedChars[idx]);
            }
            if (r < rows - 1)
                sb.AppendLine();
        }

        return sb.ToString();
    }
}
