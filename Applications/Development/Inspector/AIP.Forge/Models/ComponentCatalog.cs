namespace AIPForge.Models;

public sealed record ComponentMeta(string Title, string Category, string Description);

/// <summary>
/// AIP 컴포넌트(cid) → 한국어 이름 · 분류 · 설명.
/// Advanced Installer GUI 왼쪽 메뉴 구성과 비슷하게 묶어 처음 보는 사람도 위치를 짐작할 수 있게 한다.
/// </summary>
public static class ComponentCatalog
{
    public const string CatProduct = "제품 정보";
    public const string CatFiles = "파일 · 폴더";
    public const string CatSystem = "시스템 변경";
    public const string CatSearch = "검색 · 조건";
    public const string CatCustom = "사용자 지정 작업";
    public const string CatUi = "사용자 인터페이스";
    public const string CatBuild = "빌드 · 패키지";
    public const string CatEtc = "기타";

    public static readonly string[] CategoryOrder = [CatProduct, CatFiles, CatSystem, CatSearch, CatCustom, CatUi, CatBuild, CatEtc];

    private static readonly Dictionary<string, ComponentMeta> Map = new()
    {
        // 제품 정보
        ["MsiPropsComponent"] = new("속성 (Property)", CatProduct, "제품명·버전·제조사·ProductCode 등 MSI Property 테이블"),
        ["MsiUpgradeComponent"] = new("업그레이드", CatProduct, "이전 버전 감지·제거 규칙 (Upgrade 테이블)"),
        ["MsiLaunchConditionsComponent"] = new("실행 조건", CatProduct, "설치를 시작하기 위한 조건 (LaunchCondition)"),
        ["MsiIconsComponent"] = new("아이콘", CatProduct, "제어판·바로가기에 쓰는 아이콘"),
        ["DigCertStoreComponent"] = new("디지털 서명", CatProduct, "코드 서명 인증서 설정"),
        ["MsiSummaryInfoComponent"] = new("요약 정보", CatProduct, "MSI Summary Information"),

        // 파일 · 폴더
        ["MsiDirsComponent"] = new("디렉터리", CatFiles, "설치 대상 폴더 구조 (Directory 테이블)"),
        ["MsiFilesComponent"] = new("파일", CatFiles, "설치할 파일과 원본 경로 (File 테이블)"),
        ["MsiCompsComponent"] = new("구성 요소", CatFiles, "MSI Component — 파일·레지스트리의 설치 단위"),
        ["MsiFeatsComponent"] = new("기능 (Feature)", CatFiles, "설치 기능 트리"),
        ["MsiFeatCompsComponent"] = new("기능 ↔ 구성 요소", CatFiles, "어떤 기능이 어떤 구성 요소를 설치하는지"),
        ["MsiCreateFolderComponent"] = new("빈 폴더 생성", CatFiles, "파일 없이 만들어야 하는 폴더"),
        ["MsiRemoveFileComponent"] = new("파일 삭제", CatFiles, "설치·제거 시 지울 파일"),
        ["SynchronizedFolderComponent"] = new("폴더 동기화", CatFiles, "원본 폴더와 자동 동기화되는 설치 폴더"),
        ["MsiShortsComponent"] = new("바로가기", CatFiles, "바탕화면·시작 메뉴 바로가기"),
        ["MsiMoveFileComponent"] = new("파일 이동/복사", CatFiles, "설치 중 파일 이동·복사"),
        ["MsiDuplicateFileComponent"] = new("파일 복제", CatFiles, "설치된 파일의 복사본"),
        ["MsiLockPermComponent"] = new("권한", CatFiles, "파일·폴더·레지스트리 권한"),
        ["AI_CustomPermissionComponent"] = new("사용자 지정 권한", CatFiles, "확장 권한 설정"),

        // 시스템 변경
        ["MsiRegsComponent"] = new("레지스트리", CatSystem, "설치할 레지스트리 값"),
        ["MsiServInstComponent"] = new("서비스 설치", CatSystem, "Windows 서비스 등록"),
        ["MsiServCtrlComponent"] = new("서비스 제어", CatSystem, "설치·제거 시 서비스 시작/중지/삭제"),
        ["MsiEnvComponent"] = new("환경 변수", CatSystem, "PATH 등 환경 변수"),
        ["MsiIniFileComponent"] = new("INI 파일", CatSystem, "INI 파일 항목"),
        ["MsiFontsComponent"] = new("글꼴", CatSystem, "등록할 글꼴"),
        ["MsiExtsComponent"] = new("파일 확장자", CatSystem, "확장자 연결"),
        ["MsiVerbsComponent"] = new("확장자 동작", CatSystem, "확장자별 열기·편집 동작"),
        ["MsiProgIdComponent"] = new("ProgID", CatSystem, "COM ProgID"),
        ["MsiClassComponent"] = new("COM 클래스", CatSystem, "COM 클래스 등록"),
        ["MsiTypeLibComponent"] = new("형식 라이브러리", CatSystem, "TypeLib 등록"),
        ["FirewallExceptionComponent"] = new("방화벽 예외", CatSystem, "Windows 방화벽 규칙"),
        ["ScheduledTaskComponent"] = new("예약 작업", CatSystem, "작업 스케줄러 등록"),
        ["XmlFileComponent"] = new("XML 파일 편집", CatSystem, "설치 중 XML 파일 수정"),
        ["XmlElementComponent"] = new("XML 요소", CatSystem, "XML 요소 편집 규칙"),
        ["XmlAttributeComponent"] = new("XML 속성", CatSystem, "XML 속성 편집 규칙"),
        ["JsonFileComponent"] = new("JSON 파일 편집", CatSystem, "설치 중 JSON 파일 수정"),
        ["IISWebSiteComponent"] = new("IIS 웹 사이트", CatSystem, "IIS 사이트 구성"),
        ["IISAppPoolComponent"] = new("IIS 앱 풀", CatSystem, "IIS 애플리케이션 풀"),

        // 검색 · 조건
        ["MsiAppSearchComponent"] = new("앱 검색", CatSearch, "설치 전에 값을 찾아 Property에 넣는 검색 (AppSearch)"),
        ["MsiRegLocatorComponent"] = new("레지스트리 검색", CatSearch, "레지스트리 값 검색 (RegLocator)"),
        ["MsiDrLocatorComponent"] = new("폴더 검색", CatSearch, "폴더 검색 (DrLocator)"),
        ["MsiCompLocatorComponent"] = new("구성 요소 검색", CatSearch, "설치된 구성 요소 검색 (CompLocator)"),
        ["MsiSignatureComponent"] = new("파일 서명 검색", CatSearch, "파일 검색 조건 (Signature)"),
        ["MsiIniLocatorComponent"] = new("INI 검색", CatSearch, "INI 값 검색"),
        ["PreReqSearchComponent"] = new("선행 조건 검색", CatSearch, "선행 조건 설치 여부 판정"),
        ["MsiConditionComponent"] = new("기능 조건", CatSearch, "기능 설치 수준 조건"),

        // 사용자 지정 작업
        ["MsiCustActComponent"] = new("사용자 지정 작업", CatCustom, "Custom Action 정의"),
        ["MsiInstExSeqComponent"] = new("InstallExecute 시퀀스", CatCustom, "설치 실행 단계의 작업 순서"),
        ["MsiInstallUISequenceComponent"] = new("InstallUI 시퀀스", CatCustom, "UI 단계의 작업 순서"),
        ["MsiAdminExSeqComponent"] = new("AdminExecute 시퀀스", CatCustom, "관리 설치 실행 순서"),
        ["MsiAdminUISequenceComponent"] = new("AdminUI 시퀀스", CatCustom, "관리 설치 UI 순서"),
        ["MsiAdvtExSeqComponent"] = new("AdvtExecute 시퀀스", CatCustom, "알림 설치 실행 순서"),
        ["MsiBinaryComponent"] = new("바이너리", CatCustom, "Custom Action이 쓰는 내장 바이너리"),
        ["MsiEmbeddedUIComponent"] = new("내장 UI", CatCustom, "Embedded UI"),

        // 사용자 인터페이스
        ["MsiThemeComponent"] = new("테마", CatUi, "설치 마법사 테마"),
        ["MsiDialogComponent"] = new("대화상자", CatUi, "설치 마법사 대화상자"),
        ["MsiControlComponent"] = new("컨트롤", CatUi, "대화상자 컨트롤 (Control 테이블)"),
        ["MsiControlEventComponent"] = new("컨트롤 이벤트", CatUi, "버튼 클릭 등 이벤트 (ControlEvent)"),
        ["MsiControlConditionComponent"] = new("컨트롤 조건", CatUi, "컨트롤 표시·활성 조건"),
        ["MsiEventMappingComponent"] = new("이벤트 매핑", CatUi, "진행률 등 이벤트 구독"),
        ["MsiActionTextComponent"] = new("작업 텍스트", CatUi, "진행 중 표시되는 작업 설명"),
        ["MsiTextStyleComponent"] = new("텍스트 스타일", CatUi, "UI 글꼴 스타일"),
        ["MsiRadioButtonComponent"] = new("라디오 버튼", CatUi, "라디오 버튼 항목"),
        ["MsiUITextComponent"] = new("UI 문자열", CatUi, "UI 텍스트 (UIText)"),
        ["BootstrapperUISequenceComponent"] = new("부트스트래퍼 UI 시퀀스", CatUi, "EXE 부트스트래퍼 UI 순서"),

        // 빌드 · 패키지
        ["BuildComponent"] = new("빌드 설정", CatBuild, "출력 폴더·파일명·패키지 형식·언어"),
        ["BootstrOptComponent"] = new("부트스트래퍼 옵션", CatBuild, "EXE 설치 관리자 옵션"),
        ["PreReqComponent"] = new("선행 조건", CatBuild, ".NET 런타임 등 먼저 설치할 패키지"),
        ["DictionaryComponent"] = new("사전 (언어)", CatBuild, "UI 번역 사전 파일"),
        ["FragmentComponent"] = new("프래그먼트", CatBuild, "공통 UI·시퀀스 조각"),
        ["MsiMergeModsComponent"] = new("병합 모듈", CatBuild, "Merge Module (.msm)"),
        ["UpdaterComponent"] = new("업데이터", CatBuild, "자동 업데이트 설정"),
        ["MsiPatchComponent"] = new("패치", CatBuild, "패치 설정"),
    };

    public static ComponentMeta Get(string shortCid)
    {
        if (Map.TryGetValue(shortCid, out var meta)) return meta;
        return new ComponentMeta(Prettify(shortCid), CatEtc, shortCid);
    }

    /// <summary>"MsiFooBarComponent" → "Foo Bar"</summary>
    private static string Prettify(string cid)
    {
        var s = cid;
        if (s.StartsWith("Msi", StringComparison.Ordinal)) s = s[3..];
        if (s.EndsWith("Component", StringComparison.Ordinal)) s = s[..^9];
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            if (i > 0 && char.IsUpper(s[i]) && !char.IsUpper(s[i - 1])) sb.Append(' ');
            sb.Append(s[i]);
        }
        return sb.Length == 0 ? cid : sb.ToString();
    }
}
