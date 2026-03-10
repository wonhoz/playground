namespace SpecView.Services;

public class ExportService
{
    // ── Markdown ─────────────────────────────────────────────────────

    public string ToMarkdown(HardwareData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# 시스템 스펙 — {d.ComputerName}");
        sb.AppendLine($"> 스캔: {d.ScannedAt:yyyy-MM-dd HH:mm:ss}  |  OS: {d.OsCaption} ({d.OsVersion})");
        sb.AppendLine();

        if (d.Cpu is { } cpu)
        {
            sb.AppendLine("## CPU");
            sb.AppendLine($"| 항목 | 값 |");
            sb.AppendLine($"|------|-----|");
            sb.AppendLine($"| 모델 | {cpu.Name} |");
            sb.AppendLine($"| 소켓 | {cpu.Socket} |");
            sb.AppendLine($"| 코어 / 스레드 | {cpu.Cores}C / {cpu.Threads}T |");
            sb.AppendLine($"| 최대 클럭 | {cpu.ClockDisplay} |");
            if (!string.IsNullOrEmpty(cpu.L3Cache)) sb.AppendLine($"| L3 캐시 | {cpu.L3Cache} |");
            sb.AppendLine();
        }

        var mem = d.Memory;
        sb.AppendLine("## 메모리");
        sb.AppendLine($"| 항목 | 값 |");
        sb.AppendLine($"|------|-----|");
        sb.AppendLine($"| 합계 | {mem.TotalDisplay} |");
        sb.AppendLine($"| 슬롯 | {mem.SlotDisplay} |");
        if (mem.MaxSpeedMHz > 0) sb.AppendLine($"| 속도 | {mem.MaxSpeedMHz} MHz |");
        sb.AppendLine();

        if (d.Gpus.Count > 0)
        {
            sb.AppendLine("## GPU");
            foreach (var gpu in d.Gpus)
            {
                sb.AppendLine($"### {gpu.Name}");
                sb.AppendLine($"| 항목 | 값 |");
                sb.AppendLine($"|------|-----|");
                sb.AppendLine($"| VRAM | {gpu.VramDisplay} |");
                sb.AppendLine($"| 드라이버 | {gpu.DriverVersion} ({gpu.DriverDate}) |");
                if (!string.IsNullOrEmpty(gpu.VideoModeDescription))
                    sb.AppendLine($"| 현재 해상도 | {gpu.VideoModeDescription} |");
                sb.AppendLine();
            }
        }

        var board = d.Board;
        sb.AppendLine("## 마더보드");
        sb.AppendLine($"| 항목 | 값 |");
        sb.AppendLine($"|------|-----|");
        sb.AppendLine($"| 제조사 | {board.Manufacturer} |");
        sb.AppendLine($"| 제품 | {board.Product} |");
        sb.AppendLine($"| BIOS | {board.BiosVersion} ({board.BiosDate}) |");
        sb.AppendLine();

        if (d.Drives.Count > 0)
        {
            sb.AppendLine("## 저장장치");
            sb.AppendLine("| 모델 | 용량 | 인터페이스 | S.M.A.R.T |");
            sb.AppendLine("|------|------|-----------|----------|");
            foreach (var drv in d.Drives)
                sb.AppendLine($"| {drv.Model} | {drv.SizeDisplay} | {drv.InterfaceType} | {drv.SmartStatus} |");
            sb.AppendLine();
        }

        if (d.Networks.Count > 0)
        {
            sb.AppendLine("## 네트워크 어댑터");
            sb.AppendLine("| 이름 | MAC 주소 | 상태 | 속도 |");
            sb.AppendLine("|------|---------|------|------|");
            foreach (var net in d.Networks)
                sb.AppendLine($"| {net.Description} | {net.MACAddress} | {net.ConnectionStatus} | {net.Speed} |");
            sb.AppendLine();
        }

        return sb.ToString();
    }

    // ── HTML ─────────────────────────────────────────────────────────

    public string ToHtml(HardwareData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine("""
            <!DOCTYPE html>
            <html lang="ko"><head>
            <meta charset="UTF-8">
            <title>Spec.View — 시스템 스펙 리포트</title>
            <style>
            body { font-family: 'Segoe UI', sans-serif; background:#0B0F1A; color:#E2E8F0; margin:0; padding:24px; }
            h1 { color:#00C8E0; border-bottom:1px solid #1E293B; padding-bottom:8px; }
            h2 { color:#00C8E0; margin-top:28px; font-size:1rem; text-transform:uppercase; letter-spacing:.1em; }
            .meta { color:#64748B; font-size:.875rem; margin-bottom:24px; }
            table { width:100%; border-collapse:collapse; margin-bottom:16px; }
            th,td { padding:8px 12px; border:1px solid #1E293B; text-align:left; }
            th { background:#111827; color:#94A3B8; font-weight:600; font-size:.8rem; }
            td { background:#0D1524; }
            .ok { color:#10B981; } .warn { color:#F59E0B; } .bad { color:#EF4444; }
            .card { background:#111827; border:1px solid #1E293B; border-radius:8px; padding:16px; margin-bottom:16px; }
            .grid2 { display:grid; grid-template-columns:1fr 1fr; gap:16px; }
            </style></head><body>
            """);

        sb.AppendLine($"<h1>💻 시스템 스펙 리포트</h1>");
        sb.AppendLine($"<p class='meta'>컴퓨터: <strong>{d.ComputerName}</strong> &nbsp;|&nbsp; OS: {d.OsCaption} ({d.OsVersion}) &nbsp;|&nbsp; 스캔: {d.ScannedAt:yyyy-MM-dd HH:mm:ss}</p>");

        if (d.Cpu is { } cpu)
        {
            sb.AppendLine("<div class='card'><h2>🔲 CPU</h2>");
            sb.AppendLine("<table><tr><th>항목</th><th>값</th></tr>");
            sb.AppendLine($"<tr><td>모델</td><td>{cpu.Name}</td></tr>");
            sb.AppendLine($"<tr><td>소켓</td><td>{cpu.Socket}</td></tr>");
            sb.AppendLine($"<tr><td>코어 / 스레드</td><td>{cpu.Cores}C / {cpu.Threads}T</td></tr>");
            sb.AppendLine($"<tr><td>최대 클럭</td><td>{cpu.ClockDisplay}</td></tr>");
            if (!string.IsNullOrEmpty(cpu.L3Cache)) sb.AppendLine($"<tr><td>L3 캐시</td><td>{cpu.L3Cache}</td></tr>");
            sb.AppendLine("</table></div>");
        }

        var mem = d.Memory;
        sb.AppendLine("<div class='card'><h2>🧮 메모리</h2><table><tr><th>항목</th><th>값</th></tr>");
        sb.AppendLine($"<tr><td>합계</td><td>{mem.TotalDisplay}</td></tr>");
        sb.AppendLine($"<tr><td>슬롯</td><td>{mem.SlotDisplay}</td></tr>");
        if (mem.MaxSpeedMHz > 0) sb.AppendLine($"<tr><td>속도</td><td>{mem.MaxSpeedMHz} MHz</td></tr>");
        sb.AppendLine("</table></div>");

        foreach (var gpu in d.Gpus)
        {
            sb.AppendLine($"<div class='card'><h2>🖥 GPU — {gpu.Name}</h2><table><tr><th>항목</th><th>값</th></tr>");
            sb.AppendLine($"<tr><td>VRAM</td><td>{gpu.VramDisplay}</td></tr>");
            sb.AppendLine($"<tr><td>드라이버</td><td>{gpu.DriverVersion} ({gpu.DriverDate})</td></tr>");
            if (!string.IsNullOrEmpty(gpu.VideoModeDescription))
                sb.AppendLine($"<tr><td>현재 해상도</td><td>{gpu.VideoModeDescription}</td></tr>");
            sb.AppendLine("</table></div>");
        }

        var board = d.Board;
        sb.AppendLine("<div class='card'><h2>🔧 마더보드</h2><table><tr><th>항목</th><th>값</th></tr>");
        sb.AppendLine($"<tr><td>제조사</td><td>{board.Manufacturer}</td></tr>");
        sb.AppendLine($"<tr><td>제품</td><td>{board.Product}</td></tr>");
        sb.AppendLine($"<tr><td>BIOS</td><td>{board.BiosVersion} ({board.BiosDate})</td></tr>");
        sb.AppendLine("</table></div>");

        if (d.Drives.Count > 0)
        {
            sb.AppendLine("<div class='card'><h2>💾 저장장치</h2>");
            sb.AppendLine("<table><tr><th>모델</th><th>용량</th><th>인터페이스</th><th>S.M.A.R.T</th></tr>");
            foreach (var drv in d.Drives)
            {
                var cls = drv.SmartOk ? "ok" : "bad";
                sb.AppendLine($"<tr><td>{drv.Model}</td><td>{drv.SizeDisplay}</td><td>{drv.InterfaceType}</td><td class='{cls}'>{drv.SmartStatus}</td></tr>");
            }
            sb.AppendLine("</table></div>");
        }

        if (d.Networks.Count > 0)
        {
            sb.AppendLine("<div class='card'><h2>🌐 네트워크</h2>");
            sb.AppendLine("<table><tr><th>이름</th><th>MAC</th><th>상태</th><th>속도</th></tr>");
            foreach (var net in d.Networks)
            {
                var cls = net.IsConnected ? "ok" : "";
                sb.AppendLine($"<tr><td>{net.Description}</td><td>{net.MACAddress}</td><td class='{cls}'>{net.ConnectionStatus}</td><td>{net.Speed}</td></tr>");
            }
            sb.AppendLine("</table></div>");
        }

        sb.AppendLine("</body></html>");
        return sb.ToString();
    }

    // ── 텍스트 (클립보드용) ──────────────────────────────────────────

    public string ToText(HardwareData d)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== {d.ComputerName} 시스템 스펙 ({d.ScannedAt:yyyy-MM-dd HH:mm}) ===");
        sb.AppendLine($"OS: {d.OsCaption}");
        sb.AppendLine();

        if (d.Cpu is { } cpu)
            sb.AppendLine($"CPU: {cpu.Name} | {cpu.Cores}C/{cpu.Threads}T | {cpu.ClockDisplay} | {cpu.Socket}");

        var mem = d.Memory;
        sb.AppendLine($"RAM: {mem.TotalDisplay} {(mem.MaxSpeedMHz > 0 ? $"@ {mem.MaxSpeedMHz}MHz" : "")} | {mem.SlotDisplay}");

        foreach (var gpu in d.Gpus)
            sb.AppendLine($"GPU: {gpu.Name} | VRAM {gpu.VramDisplay} | Driver {gpu.DriverVersion}");

        var b = d.Board;
        sb.AppendLine($"MB:  {b.Manufacturer} {b.Product} | BIOS {b.BiosVersion} ({b.BiosDate})");

        foreach (var drv in d.Drives)
            sb.AppendLine($"HDD: {drv.Model} | {drv.SizeDisplay} | {drv.InterfaceType} | {drv.SmartStatus}");

        return sb.ToString();
    }
}
