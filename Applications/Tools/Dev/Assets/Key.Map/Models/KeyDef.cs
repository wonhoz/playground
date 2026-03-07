namespace Key.Map.Models;

/// <summary>키보드 레이아웃 내 단일 키 정의</summary>
/// <param name="Code">ShortcutEntry.Keys 파싱 시 매핑 코드</param>
/// <param name="Label">키 표면에 표시할 텍스트</param>
/// <param name="X">열 위치 (단위 = 1개 표준 키 폭)</param>
/// <param name="Y">행 위치 (단위 = 1개 표준 키 높이)</param>
/// <param name="W">키 폭 배수 (기본 1.0)</param>
/// <param name="H">키 높이 배수 (기본 1.0)</param>
record KeyDef(string Code, string Label, double X, double Y, double W = 1.0, double H = 1.0);
