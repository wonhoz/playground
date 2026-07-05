using System.Globalization;
using System.IO;
using Stock.Catch.Models;

namespace Stock.Catch.Services;

/// <summary>분봉 CSV(헤더 date,time,open,close,low,high,volume · 순서 무관) 읽기 공용 유틸.</summary>
public static class MinuteCsvIo
{
    /// <summary>분봉 CSV → Candle 목록(시각 오름차순). 형식 오류 시 예외.</summary>
    public static List<Candle> Parse(string path)
    {
        var lines = File.ReadAllLines(path);
        if (lines.Length < 2) throw new InvalidDataException("CSV에 데이터 행이 없습니다.");

        var header = lines[0].TrimStart('﻿').Split(',').Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Idx(string key) => header.IndexOf(key) is var i && i >= 0
            ? i
            : throw new InvalidDataException($"분봉 CSV 형식이 아닙니다 — '{key}' 컬럼이 없습니다(필수: date,time,open,close,low,high,volume).");
        int di = Idx("date"), ti = Idx("time"), oi = Idx("open"), ci = Idx("close"), li = Idx("low"), hi = Idx("high"), vi = Idx("volume");

        var inv = CultureInfo.InvariantCulture;
        var bars = new List<Candle>();
        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var f = line.Split(',');
            if (f.Length <= Math.Max(vi, Math.Max(hi, Math.Max(li, Math.Max(ci, Math.Max(oi, ti)))))) continue;
            if (!DateTime.TryParse($"{f[di].Trim()} {f[ti].Trim()}", inv, DateTimeStyles.None, out var dt)) continue;
            if (!decimal.TryParse(f[oi], NumberStyles.Number, inv, out var o) ||
                !decimal.TryParse(f[ci], NumberStyles.Number, inv, out var c) ||
                !decimal.TryParse(f[li], NumberStyles.Number, inv, out var lo) ||
                !decimal.TryParse(f[hi], NumberStyles.Number, inv, out var h)) continue;
            long.TryParse(f[vi], NumberStyles.Number, inv, out var v);
            bars.Add(new Candle(dt, o, h, lo, c, v));
        }
        if (bars.Count == 0)
            throw new InvalidDataException("파싱된 분봉이 없습니다. [🕐 분봉 CSV]로 저장한 파일인지 확인하세요.");
        return bars.OrderBy(b => b.Date).ToList();
    }

    /// <summary>파일 stem "이름(코드)_yyyyMMdd_..."에서 코드·이름·날짜 추출(실패 시 null).</summary>
    public static (string Code, string Name, DateTime Date)? ParseStem(string path)
    {
        var m = System.Text.RegularExpressions.Regex.Match(
            Path.GetFileNameWithoutExtension(path), @"^(.*)\(([0-9A-Za-z]{5,6})\)_(\d{8})_");
        if (!m.Success) return null;
        if (!DateTime.TryParseExact(m.Groups[3].Value, "yyyyMMdd", null, DateTimeStyles.None, out var d)) return null;
        return (m.Groups[2].Value.ToUpperInvariant(), m.Groups[1].Value.Trim(), d);
    }
}
