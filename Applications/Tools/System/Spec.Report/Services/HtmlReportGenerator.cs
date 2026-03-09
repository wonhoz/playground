namespace SpecReport.Services;

public class HtmlReportGenerator
{
    // ── 단일 리포트 HTML 생성 ─────────────────────────────
    public string Generate(SystemReport r)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHead($"Spec.Report — {r.ComputerName}"));
        sb.Append("<body><div class=\"container\">");

        // 헤더
        sb.Append($"""
            <div class="header">
                <div class="header-title">📊 Spec.Report</div>
                <div class="header-sub">{r.ComputerName} &nbsp;·&nbsp; {r.UserName} &nbsp;·&nbsp; 수집: {r.CollectedAt:yyyy-MM-dd HH:mm:ss}</div>
            </div>
            """);

        // CPU
        sb.Append(SectionCard("CPU", "🖥️", new[]
        {
            Row("프로세서",   r.Cpu.Name),
            Row("제조사",    r.Cpu.Manufacturer),
            Row("물리 코어", $"{r.Cpu.PhysicalCores}코어 / {r.Cpu.LogicalCores}스레드"),
            Row("최대 클럭", $"{r.Cpu.MaxClockGHz:F2} GHz"),
            Row("아키텍처",  r.Cpu.Architecture),
            Row("소켓",      r.Cpu.Socket)
        }));

        // RAM
        var ramContent = new StringBuilder();
        ramContent.Append(Row("총 용량", JsonReportService.FormatBytes(r.TotalRamBytes)));
        ramContent.Append(Row("슬롯 수", $"{r.RamSlots.Count}개"));
        foreach (var slot in r.RamSlots)
            ramContent.Append(Row($"  {slot.Slot}",
                $"{JsonReportService.FormatBytes(slot.CapacityBytes)} {slot.MemoryType}-{slot.SpeedMHz} &nbsp;{slot.Manufacturer}"));
        sb.Append(SectionCard("RAM", "🧠", ramContent));

        // GPU
        foreach (var (gpu, i) in r.Gpus.Select((g, i) => (g, i)))
        {
            sb.Append(SectionCard(r.Gpus.Count > 1 ? $"GPU #{i + 1}" : "GPU", "🎮", new[]
            {
                Row("모델",        gpu.Name),
                Row("VRAM",        JsonReportService.FormatBytes(gpu.VramBytes)),
                Row("드라이버",    gpu.DriverVersion),
                Row("드라이버 날짜", gpu.DriverDate),
                Row("해상도",      $"{gpu.CurrentWidth} × {gpu.CurrentHeight} @ {gpu.RefreshRate}Hz")
            }));
        }

        // 스토리지
        var storContent = new StringBuilder();
        foreach (var d in r.Drives)
        {
            var pct = d.TotalBytes > 0 ? (double)(d.TotalBytes - d.FreeBytes) / d.TotalBytes * 100 : 0;
            storContent.Append($"""
                <div class="drive-row">
                    <div class="drive-header">
                        <span class="drive-letter">{d.DriveLetter}</span>
                        <span class="drive-label">{(string.IsNullOrEmpty(d.Label) ? "로컬 디스크" : d.Label)}</span>
                        <span class="drive-type {(d.MediaType.Contains("NVMe") ? "c-green" : d.MediaType == "SSD" ? "c-blue" : "c-dim")}">{d.MediaType}</span>
                        <span class="drive-fs c-dim">{d.FileSystem}</span>
                    </div>
                    {(string.IsNullOrEmpty(d.Model) ? "" : $"<div class=\"drive-model c-dim\">{d.Model}</div>")}
                    <div class="progress-bar">
                        <div class="progress-fill {(pct > 90 ? "c-red-bg" : pct > 70 ? "c-orange-bg" : "c-blue-bg")}"
                             style="width:{pct:F0}%"></div>
                    </div>
                    <div class="drive-size">
                        <span>{JsonReportService.FormatBytes(d.TotalBytes - d.FreeBytes)} 사용</span>
                        <span class="c-dim"> / {JsonReportService.FormatBytes(d.TotalBytes)} &nbsp; (여유 {JsonReportService.FormatBytes(d.FreeBytes)})</span>
                    </div>
                </div>
                """);
        }
        sb.Append(SectionCard("스토리지", "💾", storContent));

        // OS
        var uptime = DateTime.Now - r.Os.LastBoot;
        sb.Append(SectionCard("운영체제", "🪟", new[]
        {
            Row("이름",       r.Os.Caption),
            Row("버전",       r.Os.Version),
            Row("빌드 번호",  r.Os.BuildNumber),
            Row("아키텍처",   r.Os.Architecture),
            Row("설치 날짜",  r.Os.InstallDate == default ? "─" : r.Os.InstallDate.ToString("yyyy-MM-dd")),
            Row("마지막 부팅", r.Os.LastBoot == default ? "─" : $"{r.Os.LastBoot:yyyy-MM-dd HH:mm}  (가동 {(int)uptime.TotalDays}일 {uptime.Hours}시간)"),
            Row("등록 사용자", r.Os.RegisteredOwner),
            Row(".NET 버전",  r.Os.DotNetVersion),
            Row("마지막 업데이트", r.Os.WindowsUpdateDate)
        }));

        // 네트워크
        var netContent = new StringBuilder();
        foreach (var a in r.NetworkAdapters)
        {
            netContent.Append($"<div class=\"adapter-name\">{(a.IsWireless ? "📶" : "🔌")} {a.Description}</div>");
            foreach (var ip in a.IpAddresses)
                netContent.Append(Row("IP", ip));
            netContent.Append(Row("MAC", a.MacAddress));
            if (a.DnsServers.Count > 0)
                netContent.Append(Row("DNS", string.Join(", ", a.DnsServers)));
            netContent.Append(Row("속도", a.Speed));
            netContent.Append("<hr class=\"inner-hr\"/>");
        }
        sb.Append(SectionCard("네트워크", "🌐", netContent));

        // 보안
        sb.Append(SectionCard("보안", "🔒", new[]
        {
            StatusRow("Windows Defender",  r.Security.DefenderEnabled,
                r.Security.DefenderEnabled ? $"활성 ({r.Security.DefenderProduct})" : "비활성", true),
            StatusRow("방화벽",            r.Security.FirewallEnabled,
                r.Security.FirewallEnabled ? "활성" : "비활성", true),
            StatusRow("BitLocker",
                r.Security.BitLockerStatus == "On",
                r.Security.BitLockerStatus,
                r.Security.BitLockerStatus != "Unknown"),
            StatusRow("자동 업데이트",     r.Security.AutoUpdateEnabled,
                r.Security.AutoUpdateEnabled ? "활성" : "비활성", true)
        }));

        // 설치 소프트웨어
        var swSb = new StringBuilder();
        swSb.Append($"<div class=\"sw-count\">{r.Software.Count}개 설치됨</div>");
        swSb.Append("<table class=\"sw-table\"><tr><th>이름</th><th>버전</th><th>게시자</th><th>설치일</th></tr>");
        foreach (var app in r.Software)
            swSb.Append($"<tr><td>{Esc(app.Name)}</td><td class=\"mono c-dim\">{Esc(app.Version)}</td><td class=\"c-dim\">{Esc(app.Publisher)}</td><td class=\"c-dim\">{app.InstallDate}</td></tr>");
        swSb.Append("</table>");
        sb.Append(SectionCard("설치 소프트웨어", "📦", swSb));

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // ── 비교 HTML 생성 ────────────────────────────────────
    public string GenerateCompare(CompareResult cr)
    {
        var sb = new StringBuilder();
        sb.Append(HtmlHead($"Spec.Report 비교 — {cr.Old.ComputerName}"));
        sb.Append("<body><div class=\"container\">");

        sb.Append($"""
            <div class="header">
                <div class="header-title">🔍 Spec.Report — 비교</div>
                <div class="header-sub">
                    이전: {cr.Old.CollectedAt:yyyy-MM-dd HH:mm}
                    &nbsp;→&nbsp;
                    이후: {cr.New.CollectedAt:yyyy-MM-dd HH:mm}
                    &nbsp;·&nbsp; {cr.Old.ComputerName}
                </div>
            </div>
            """);

        // 변경 사항 요약
        var totalChanges = cr.Changes.Count + cr.AddedSoftware.Count +
                           cr.RemovedSoftware.Count + cr.UpdatedSoftware.Count;
        sb.Append($"""
            <div class="card">
                <div class="section-title">📊 요약</div>
                <div class="summary-grid">
                    <div class="summary-item"><span class="big-num c-blue">{cr.Changes.Count}</span><div>설정 변경</div></div>
                    <div class="summary-item"><span class="big-num c-green">{cr.AddedSoftware.Count}</span><div>추가된 소프트웨어</div></div>
                    <div class="summary-item"><span class="big-num c-orange">{cr.UpdatedSoftware.Count}</span><div>업데이트된 소프트웨어</div></div>
                    <div class="summary-item"><span class="big-num c-red">{cr.RemovedSoftware.Count}</span><div>삭제된 소프트웨어</div></div>
                </div>
            </div>
            """);

        if (cr.Changes.Count > 0)
        {
            var diffSb = new StringBuilder();
            diffSb.Append("<table class=\"diff-table\"><tr><th>섹션</th><th>항목</th><th>이전</th><th>이후</th></tr>");
            foreach (var c in cr.Changes)
                diffSb.Append($"<tr><td class=\"c-dim\">{Esc(c.Section)}</td><td>{Esc(c.Field)}</td>" +
                               $"<td class=\"c-red mono\">{Esc(c.OldValue)}</td>" +
                               $"<td class=\"c-green mono\">{Esc(c.NewValue)}</td></tr>");
            diffSb.Append("</table>");
            sb.Append(SectionCard("설정 변경", "⚙️", diffSb));
        }

        if (cr.AddedSoftware.Count > 0)
        {
            var aSb = new StringBuilder();
            aSb.Append("<table class=\"sw-table\"><tr><th>이름</th><th>버전</th><th>게시자</th></tr>");
            foreach (var a in cr.AddedSoftware)
                aSb.Append($"<tr class=\"row-added\"><td>{Esc(a.Name)}</td><td class=\"mono\">{Esc(a.Version)}</td><td class=\"c-dim\">{Esc(a.Publisher)}</td></tr>");
            aSb.Append("</table>");
            sb.Append(SectionCard($"추가된 소프트웨어 ({cr.AddedSoftware.Count}개)", "➕", aSb));
        }

        if (cr.UpdatedSoftware.Count > 0)
        {
            var uSb = new StringBuilder();
            uSb.Append("<table class=\"sw-table\"><tr><th>이름</th><th>이전 버전</th><th>이후 버전</th></tr>");
            foreach (var u in cr.UpdatedSoftware)
                uSb.Append($"<tr class=\"row-updated\"><td>{Esc(u.Name)}</td><td class=\"mono c-red\">{Esc(u.OldVersion)}</td><td class=\"mono c-green\">{Esc(u.NewVersion)}</td></tr>");
            uSb.Append("</table>");
            sb.Append(SectionCard($"업데이트된 소프트웨어 ({cr.UpdatedSoftware.Count}개)", "🔄", uSb));
        }

        if (cr.RemovedSoftware.Count > 0)
        {
            var rSb = new StringBuilder();
            rSb.Append("<table class=\"sw-table\"><tr><th>이름</th><th>버전</th><th>게시자</th></tr>");
            foreach (var a in cr.RemovedSoftware)
                rSb.Append($"<tr class=\"row-removed\"><td>{Esc(a.Name)}</td><td class=\"mono\">{Esc(a.Version)}</td><td class=\"c-dim\">{Esc(a.Publisher)}</td></tr>");
            rSb.Append("</table>");
            sb.Append(SectionCard($"삭제된 소프트웨어 ({cr.RemovedSoftware.Count}개)", "➖", rSb));
        }

        if (totalChanges == 0)
            sb.Append("<div class=\"card\"><p class=\"c-green\" style=\"text-align:center;padding:20px\">✅ 두 리포트 간 변경 사항이 없습니다.</p></div>");

        sb.Append("</div></body></html>");
        return sb.ToString();
    }

    // ── 헬퍼 ─────────────────────────────────────────────
    private static string Row(string label, string? value)
        => $"""<div class="row"><span class="label">{label}</span><span class="value mono">{Esc(value ?? "─")}</span></div>""";

    private static string StatusRow(string label, bool ok, string value, bool warn)
    {
        var cls = ok ? "c-green" : (warn ? "c-red" : "c-dim");
        var dot = ok ? "●" : "○";
        return $"""<div class="row"><span class="label">{label}</span><span class="value mono {cls}">{dot} {Esc(value)}</span></div>""";
    }

    private static string SectionCard(string title, string icon, IEnumerable<string> rows)
    {
        var sb = new StringBuilder();
        sb.Append($"<div class=\"card\"><div class=\"section-title\">{icon} {title}</div>");
        foreach (var r in rows) sb.Append(r);
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string SectionCard(string title, string icon, StringBuilder content)
        => $"<div class=\"card\"><div class=\"section-title\">{icon} {title}</div>{content}</div>";

    private static string Esc(string s) => s
        .Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;");

    private const string HtmlCss = @"
*{box-sizing:border-box;margin:0;padding:0}
body{background:#12121E;color:#E0E0F0;font-family:'Segoe UI',sans-serif;font-size:13px;line-height:1.5}
.container{max-width:960px;margin:0 auto;padding:24px 16px}
.header{background:#1A1A2A;border:1px solid #2E2E50;border-radius:10px;padding:16px 20px;margin-bottom:16px}
.header-title{font-size:20px;font-weight:700;color:#E0E0F0;margin-bottom:4px}
.header-sub{color:#606080;font-size:12px}
.card{background:#1E1E30;border:1px solid #2E2E50;border-radius:10px;padding:16px 20px;margin-bottom:12px}
.section-title{font-size:10px;text-transform:uppercase;letter-spacing:2px;color:#3280FF;margin-bottom:12px;font-weight:600}
.row{display:flex;justify-content:space-between;padding:5px 0;border-bottom:1px solid #1A1A2A;gap:16px}
.row:last-child{border-bottom:none}
.label{color:#606080;flex-shrink:0;min-width:120px}
.value{color:#E0E0F0;text-align:right;word-break:break-word}
.mono{font-family:Consolas,'Courier New',monospace}
.c-blue{color:#3280FF}.c-green{color:#50DC78}.c-orange{color:#FF9040}.c-red{color:#FF5060}.c-dim{color:#606080}
.c-blue-bg{background:#3280FF}.c-orange-bg{background:#FF9040}.c-red-bg{background:#FF5060}
.drive-row{margin-bottom:14px;padding-bottom:14px;border-bottom:1px solid #1A1A2A}
.drive-row:last-child{margin-bottom:0;padding-bottom:0;border-bottom:none}
.drive-header{display:flex;gap:10px;align-items:center;margin-bottom:6px}
.drive-letter{font-weight:700;font-size:15px;color:#E0E0F0;font-family:Consolas,'Courier New',monospace}
.drive-label{color:#A0A0CC}.drive-type{font-size:11px;padding:1px 6px;border-radius:3px;background:#252545}
.drive-fs{font-size:11px}.drive-model{font-size:11px;margin-bottom:4px}
.drive-size{font-size:11px;margin-top:3px;color:#A0A0CC}
.progress-bar{height:5px;background:#252545;border-radius:3px;overflow:hidden;margin:4px 0}
.progress-fill{height:100%;border-radius:3px;transition:width .3s}
.adapter-name{font-weight:600;color:#A0A0CC;margin:8px 0 4px}
.inner-hr{border:none;border-top:1px solid #1A1A2A;margin:8px 0}
.sw-count{color:#606080;font-size:11px;margin-bottom:8px}
.sw-table{width:100%;border-collapse:collapse;font-size:12px}
.sw-table th{color:#3280FF;text-align:left;padding:4px 8px;border-bottom:1px solid #2E2E50;font-size:10px;text-transform:uppercase;letter-spacing:1px}
.sw-table td{padding:4px 8px;border-bottom:1px solid #1A1A2A;word-break:break-word}
.sw-table tr:hover td{background:#252545}
.row-added td{color:#50DC78}.row-removed td{color:#FF5060}.row-updated td{color:#FF9040}
.diff-table{width:100%;border-collapse:collapse;font-size:12px}
.diff-table th{color:#3280FF;text-align:left;padding:4px 8px;border-bottom:1px solid #2E2E50;font-size:10px;text-transform:uppercase;letter-spacing:1px}
.diff-table td{padding:4px 8px;border-bottom:1px solid #1A1A2A}
.summary-grid{display:flex;gap:16px;flex-wrap:wrap}
.summary-item{text-align:center;min-width:80px}
.big-num{font-size:28px;font-weight:700;display:block}
@media print{body{background:#fff;color:#000}.card{border:1px solid #ccc;background:#fff}.section-title{color:#0055cc}.label{color:#555}.c-dim{color:#888}.c-blue{color:#0055cc}.c-green{color:#007a20}.c-orange{color:#c05000}.c-red{color:#c00000}}
";

    private static string HtmlHead(string title)
        => $"<!DOCTYPE html><html lang=\"ko\"><head><meta charset=\"UTF-8\">" +
           $"<meta name=\"viewport\" content=\"width=device-width,initial-scale=1\">" +
           $"<title>{Esc(title)}</title><style>{HtmlCss}</style></head>";
}
