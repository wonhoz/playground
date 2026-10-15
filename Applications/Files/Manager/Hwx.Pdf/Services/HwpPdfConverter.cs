using System.IO;
using System.Runtime.InteropServices;

namespace HwxPdf.Services;

/// <summary>
/// 설치된 한컴오피스 한글(HWPFrame.HwpObject COM)로 문서를 열어 PDF 로 저장한다.
/// 한글 COM 은 STA 스레드에서만 호출해야 한다 — 한 인스턴스를 만들어 여러 파일에 재사용한다.
/// </summary>
public sealed class HwpPdfConverter : IDisposable
{
    private const string ProgId = "HWPFrame.HwpObject";
    private dynamic? _hwp;

    private const string ModuleName = "FilePathCheckerModule";
    private const string ModulesKey = @"Software\HNC\HwpAutomation\Modules";

    public static bool IsHancomInstalled => Type.GetTypeFromProgID(ProgId) != null;

    /// <summary>보안 모듈이 등록되어 확인 창 없이 변환되는지 (첫 변환 뒤에 정해진다)</summary>
    public bool? SecurityModuleActive { get; private set; }

    /// <summary>
    /// exe 옆에 한컴 보안 모듈 DLL(FilePathCheckerModule*.dll)이 있으면 레지스트리에 경로를 기록한다.
    /// 이미 기록된 경로의 파일이 살아 있으면 그대로 둔다. 경로는 따옴표 없이 써야 한다.
    /// </summary>
    private static void EnsureSecurityModuleRegistry()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(ModulesKey);
            if (key.GetValue(ModuleName) is string current && File.Exists(current)) return;

            var dll = Directory.EnumerateFiles(AppContext.BaseDirectory, "FilePathCheckerModule*.dll").FirstOrDefault();
            if (dll != null) key.SetValue(ModuleName, dll);
        }
        catch { }
    }

    private dynamic Hwp
    {
        get
        {
            if (_hwp != null) return _hwp;
            var type = Type.GetTypeFromProgID(ProgId)
                ?? throw new InvalidOperationException("한컴오피스 한글이 설치되어 있지 않습니다.");
            dynamic hwp = Activator.CreateInstance(type)!;
            // 파일 접근 보안 확인 창을 없애는 한컴 보안 모듈 등록 — 모듈이 없으면 false, 매번 확인 창이 뜬다
            EnsureSecurityModuleRegistry();
            try { SecurityModuleActive = (bool)hwp.RegisterModule("FilePathCheckDLL", ModuleName); } catch { }
            try { hwp.XHwpWindows.Item(0).Visible = false; } catch { }
            _hwp = hwp;
            return hwp;
        }
    }

    /// <summary>hwp · hwpx · hwx 를 PDF 로 변환한다.</summary>
    public void Convert(string source, string pdfPath, string tempDir)
    {
        string? extracted = null;
        try
        {
            var input = source;
            if (string.Equals(Path.GetExtension(source), ".hwx", StringComparison.OrdinalIgnoreCase) || HwxExtractor.IsHwx(source))
                input = extracted = HwxExtractor.ExtractToTemp(source, tempDir);

            var format = Path.GetExtension(input).Equals(".hwp", StringComparison.OrdinalIgnoreCase) ? "HWP" : "";
            ConvertCore(input, format, pdfPath);
        }
        catch (COMException)
        {
            // 한글이 죽었거나 응답이 끊긴 경우 — 다음 파일은 새 인스턴스로
            Reset();
            throw;
        }
        finally
        {
            if (extracted != null)
                try { File.Delete(extracted); } catch { }
        }
    }

    private void ConvertCore(string input, string format, string pdfPath)
    {
        var hwp = Hwp;
        if (!(bool)hwp.Open(input, format, "forceopen:true;suspendpassword:true;versionwarning:false"))
            throw new InvalidOperationException("한글에서 문서를 열지 못했습니다.");
        try
        {
            if (File.Exists(pdfPath)) File.Delete(pdfPath);
            if (!(bool)hwp.SaveAs(pdfPath, "PDF", "") || !File.Exists(pdfPath))
                throw new InvalidOperationException("PDF 저장에 실패했습니다.");
        }
        finally
        {
            try { hwp.Clear(1); } catch { }   // 1 = 저장하지 않고 닫기
        }
    }

    private void Reset()
    {
        var hwp = _hwp;
        _hwp = null;
        if (hwp == null) return;
        try { hwp.Quit(); } catch { }
        try { Marshal.FinalReleaseComObject(hwp); } catch { }
    }

    public void Dispose() => Reset();
}
