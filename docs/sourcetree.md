# SourceTree "수정된 파일" 컬럼 제거 연구

## 목표

SourceTree 커밋 히스토리 뷰에서 **"수정된 파일 (Modified Files)"** 컬럼을 영구적으로 숨기거나 제거한다.

현재 문제:
- 매번 탭을 열 때마다 컬럼이 다시 표시됨
- UI에서 토글 옵션 없음
- 매번 마우스로 컬럼 너비를 0으로 드래그해야 함 (드래그로는 숨김 가능 확인됨)

---

## 환경

| 항목 | 값 |
|------|-----|
| SourceTree 버전 | 3.4.23.0 |
| 설치 방식 | Squirrel (로컬 설치) |
| 설치 경로 | `C:\Users\admin\AppData\Local\SourceTree\app-3.4.23\` |
| 프레임워크 | WPF, .NET Framework 4.8 |
| 핵심 DLL | `SourceTree.Api.UI.Wpf.dll` |

---

## 핵심 대상 파일

### 1. SourceTree.Api.UI.Wpf.dll
```
C:\Users\admin\AppData\Local\SourceTree\app-3.4.23\SourceTree.Api.UI.Wpf.dll
```
- WPF UI 컴포넌트 전체 포함 (BAML embedded resource + IL 코드)
- 백업: `SourceTree.Api.UI.Wpf.dll.bak` (동일 디렉토리)
- Playground 참고 사본: `docs/SourceTree.Api.UI.Wpf.dll/SourceTree.Api.UI.Wpf.dll`

### 2. logviewpanel.baml (DLL 내 embedded)
- 리소스 경로: `view/repo/logviewpanel.baml`
- 추출본: `C:\Users\admin\AppData\Local\Temp\logviewpanel.baml` (13887 bytes)
- "수정된 파일" 컬럼 정의가 여기에 포함됨

### 3. user.config (설정 파일)
```
C:\Users\admin\AppData\Local\Atlassian\SourceTree.exe_Url_mlgtzwub3hcm35a2425ma5ju2fkjjq4r\3.4.23.0\user.config
C:\Users\admin\AppData\Roaming\Atlassian\SourceTree.exe_Url_mlgtzwub3hcm35a2425ma5ju2fkjjq4r\3.4.23.0\user.config
```
- `ShowLogModifiedFilesColumn = False` 항목 추가 시도 → **효과 없음** (이유: 해당 설정값을 읽는 코드 없음)

---

## BAML 분석 — logviewpanel.baml

### "수정된 파일" 컬럼 BAML 위치 (DLL 기준)

| 항목 | DLL offset | BAML internal offset |
|------|-----------|---------------------|
| BAML 시작점 | `0x2027F5` | - |
| 컬럼 정의 시작 | 약 `0x205610` | `0x2E1B` 근방 |
| Width 바인딩 시작 | `0x20572B` | `0x2F36` |
| ShowLogModifiedFilesColumn path | `0x205740` | `0x2F4B` |
| **ConverterParameter "90"** | **`0x205778`** | `0x2F83` |
| FallbackValue "0" | `0x205782` | `0x2F8D` |
| DisplayMemberBinding path | `0x2057A0` | `0x2FAB` |
| ModifiedFilesCount path | `0x2057B8` | `0x2FC3` |

### BAML 컬럼 XAML 구조 (재구성)
```xml
<GridViewColumn Header="ModifiedFiles">
    <GridViewColumn.Width>
        <Binding Path="ShowLogModifiedFilesColumn"
                 Converter="{BoolToWidthConverter}"
                 ConverterParameter="90"
                 FallbackValue="0"/>
    </GridViewColumn.Width>
    <GridViewColumn.DisplayMemberBinding>
        <Binding Path="ModifiedFilesCount"/>
    </GridViewColumn.DisplayMemberBinding>
</GridViewColumn>
```

### BAML 주요 바이트 패턴 (DLL 내)
```
ConverterParameter: 24 08 4A 00 02 [39 30] → '9''0' = "90"
FallbackValue:      24 07 4B 00 01 30       → '0' = "0"
```

---

## IL 분석

### BoolToWidthConverter.Convert

| 항목 | 값 |
|------|-----|
| 위치 | DLL fat header: `0x53B08`, code body: `0x53B14` |
| MaxStack | 2 |
| CodeSize | 51 bytes |
| LocalVarSigTok | `0x110003E7` |

**Fat 헤더:**
```
13 30 02 00 33 00 00 00 E7 03 00 11
```

**IL 코드 (51 bytes):**
```
00 05 2D 03 14 2B 06 05 6F 00 01 00 0A 12 00 28 BB 0A 00 0A 26
03 A5 8C 02 00 01 2D 0B 23 00 00 00 00 00 00 00 00 2B 01 06 8C
16 02 00 01 0B 2B 00 07 2A
```

**IL 디코딩:**
```
[00] 00     nop
[01] 05     ldarg.3    → parameter 스택에 push ("90" 또는 "00")
[02] 2D 03  brtrue.s   → parameter != null이면 [07]로 jump
[04] 14     ldnull     → null 스택에 push
[05] 2B 06  br.s       → [0D]로 jump
[07] 05     ldarg.3    → parameter 다시 push
[08] 6F [00 01 00 0A]  callvirt 0x0A000100  → parameter.ToString()
[0D] 12 00  ldloca.s 0 → local0의 주소 push
[0F] 28 [BB 0A 00 0A]  call 0x0A000ABB     → double.TryParse(str, out local0)
[14] 26     pop        → TryParse 결과(bool) 버림
[15] 03     ldarg.1    → value (bool ShowLogModifiedFilesColumn) push
[16] A5 [8C 02 00 01]  isinst 0x0100028C   → System.Boolean 타입 체크
[1B] 2D 0B  brtrue.s   → IS bool이면 [28]로 jump
[1D] 23 [0.0 8bytes]   ldc.r8 0.0          → 0.0 push (NOT bool 경로)
[26] 2B 01  br.s       → [29]로 jump
[28] 06     ldloc.0    → parsedValue (IS bool 경로)
[29] 8C [16 02 00 01]  box 0x01000216      → System.Double boxing
[2E] 0B     stloc.1
[2F] 2B 00  br.s       → [31]
[31] 07     ldloc.1
[32] 2A     ret
```

**⚠️ 핵심 결론:**
```
value 인자(bool true/false)는 TYPE 체크에만 사용됨 — 실제 값은 무시됨
반환값 = double.Parse(parameter)
  parameter = "90"  → return 90.0  (컬럼 표시)
  parameter = "00"  → return 0.0   (컬럼 숨김 의도)
  value가 bool이 아니면 → return 0.0 무조건
```

---

### get_ShowLogModifiedFilesColumn (getter)

| 항목 | 값 |
|------|-----|
| Field token | `0x040001B2` (`_showLogModifiedFilesColumn`, bool) |
| Fat header offset | `0x1199C` |
| Code body offset | `0x119A8` |
| MaxStack | 1 |
| CodeSize | 12 bytes |

**Fat 헤더:**
```
13 30 01 00 0C 00 00 00 07 00 00 11
```

**원본 IL 코드:**
```
00 02 7B B2 01 00 04 0A 2B 00 06 2A
```
```
[00] 00              nop
[01] 02              ldarg.0  (this)
[02] 7B B2 01 00 04  ldfld _showLogModifiedFilesColumn
[07] 0A              stloc.0
[08] 2B 00           br.s +0
[0A] 06              ldloc.0
[0B] 2A              ret
```

**특이사항:**
- `stfld _showLogModifiedFilesColumn` (7D B2 01 00 04) 이 **DLL 전체에 존재하지 않음**
- 즉, 이 필드는 코드에서 **절대 SET되지 않음** → 항상 C# bool 기본값 = **false**
- Getter가 항상 false를 반환 → BoolToWidthConverter가 parameter("90") 값으로 Width=90 반환 → 컬럼 항상 표시

---

## 적용된 패치 (현재 DLL 상태)

### Patch 1 — BAML ConverterParameter "90" → "00"

| 항목 | 값 |
|------|-----|
| 파일 | `SourceTree.Api.UI.Wpf.dll` |
| DLL offset | `0x205778` |
| Before | `0x39` (ASCII '9') |
| After | `0x30` (ASCII '0') |
| 의도 | parameter="00" → converter가 0.0 반환 → Width=0 |
| 결과 | **컬럼 여전히 표시됨 (효과 없음)** |

### Patch 2 — get_ShowLogModifiedFilesColumn 항상 true 반환

| 항목 | 값 |
|------|-----|
| 파일 | `SourceTree.Api.UI.Wpf.dll` |
| Fat header | `0x1199C` |
| Code body offset | `0x119A8` |
| Before (bytes 0-1) | `00 02` (nop + ldarg.0) |
| After (bytes 0-1) | `17 2A` (ldc.i4.1 + ret) |
| 의도 | getter가 true 반환 → converter가 0.0 반환 (단, Patch 1과 논리적으로 동일) |
| 결과 | **컬럼 여전히 표시됨 (효과 없음)** |

**⚠️ 사후 분석**: Patch 2는 사실 BoolToWidthConverter가 `value`(bool)를 무시하기 때문에 converter 출력에 영향 없음. Patch 1(parameter="00")과 동일한 결과를 내야 하지만, 둘 다 효과 없음.

---

## 미해결 핵심 문제

### Width=0이 컬럼을 숨기지 못하는 이유 (가설)

아래 중 어느 것이 실제 원인인지 아직 미확인:

**가설 A: WPF GridViewColumn Width=0은 실제로 완전 숨김이 안 됨**
- GridViewColumnHeader 기본 스타일에 최소 픽셀(resize grip 등)이 있을 수 있음
- 단, 사용자가 마우스 드래그로는 숨김 가능하다고 확인 → Width=0 자체는 동작 가능해야 함
- Binding을 통한 Width=0 vs 직접 드래그 Width=0의 동작 차이 가능성 존재

**가설 B: Width 바인딩이 아예 적용되지 않음 (다른 코드에서 덮어씀)**
- SourceTree에 `if (ShowLogModifiedFilesColumn) column.Width = 90;` 같은 코드가 존재하고, 이게 BAML 바인딩을 무효화할 가능성
- WPF에서 DependencyProperty에 코드로 값을 직접 설정하면 binding이 제거됨
- 단, `stfld _showLogModifiedFilesColumn`가 없으므로 ShowLogModifiedFilesColumn 기반 분기는 없을 것

**가설 C: Width 바인딩 property가 GridViewColumn.Width가 아닌 다른 속성**
- BAML에서 attribute ID 0x0040이 실제로 Width가 아닐 가능성
- 인접한 다른 컬럼에서 `24 09 40 00 03 31 31 35` (Width="115") 패턴 확인 → 0x0040 = Width 맞는 것으로 보임

**가설 D: ShowLogModifiedFilesColumn 프로퍼티가 다른 DLL에도 존재**
- 패치한 DLL의 getter가 아닌 다른 ViewModel/DLL의 동명 프로퍼티가 바인딩 소스일 가능성
- 미확인 — 다른 DLL들에서 검색 필요

---

## 다음 단계 (미완료)

### 우선순위 1: Width=0 동작 검증
BoolToWidthConverter를 항상 `5.0` 반환하도록 임시 패치 후 SourceTree 재시작:
- 컬럼이 5px로 매우 좁아지면 → Width 바인딩은 동작하지만 0.0이 특별 처리됨
- 컬럼이 여전히 90px면 → 바인딩 자체가 적용 안 됨

임시 패치 방법: `BoolToWidthConverter.Convert` code body (`0x53B14`)를 다음으로 변경:
```
23 00 00 00 00 00 14 40  ldc.r8 5.0
8C 16 02 00 01           box System.Double
2A                       ret
```
(double 5.0의 IEEE 754 bytes: `00 00 00 00 00 00 14 40`)

### 우선순위 2: 다른 DLL에서 ShowLogModifiedFilesColumn 검색
```powershell
$appDir = 'C:\Users\admin\AppData\Local\SourceTree\app-3.4.23'
$pattern = [System.Text.Encoding]::Unicode.GetBytes("ShowLogModifiedFilesColumn")
Get-ChildItem $appDir -Filter '*.dll' | ForEach-Object {
    $b = [System.IO.File]::ReadAllBytes($_.FullName)
    for ($i=0; $i -lt $b.Length-$pattern.Length; $i++) {
        $match=$true; for($j=0;$j -lt $pattern.Length;$j++){if($b[$i+$j]-ne$pattern[$j]){$match=$false;break}}
        if($match){Write-Host "FOUND: $($_.Name) at 0x$($i.ToString('X'))"}
    }
}
```

### 우선순위 3: 컬럼 자체를 BAML에서 제거
Width 바인딩 조작 대신 GridViewColumn 요소 자체를 BAML에서 제거.

현재 BAML 구조 (DLL 내 offset 기준, 대략적):
```
약 0x205600-0x205BFF: "수정된 파일" GridViewColumn 전체 정의
```
정확한 ElementStart / ElementEnd 경계를 확인해야 함.

BAML record type 참고:
- `0x03` 또는 타입별로 다른 ElementStart
- `0x04` = ElementEnd (1 byte)
- `0x1F` = PropertyComplexStart
- `0x20` = PropertyComplexEnd

실제 경계는 DLL 내 BAML을 추출 후 hex 분석 필요.

### 우선순위 4: BoolToWidthConverter.Convert 자체를 항상 0 반환으로 패치
```powershell
$dllPath = 'C:\Users\admin\AppData\Local\SourceTree\app-3.4.23\SourceTree.Api.UI.Wpf.dll'
$b = [System.IO.File]::ReadAllBytes($dllPath)
# code body at 0x53B14 (51 bytes 공간)
# ldc.r8 0.0 (9 bytes) + box double (5 bytes) + ret (1 byte) = 15 bytes
$patch = @(
    0x23, 0x00,0x00,0x00,0x00,0x00,0x00,0x00,0x00,  # ldc.r8 0.0
    0x8C, 0x16,0x02,0x00,0x01,                       # box System.Double (token 0x01000216)
    0x2A                                             # ret
)
for ($i=0; $i -lt $patch.Length; $i++) { $b[0x53B14+$i] = $patch[$i] }
[System.IO.File]::WriteAllBytes($dllPath, $b)
```
단, Width=0이 실제로 숨김 효과가 없다면 이 패치도 효과 없음.

---

## 파일 구조 참고

```
docs/SourceTree.Api.UI.Wpf.dll/
├── SourceTree.Api.UI.Wpf.dll      (참고 사본)
├── SourceTree.Api.UI.Wpf.dll.bak  (원본 백업)
└── SourceTree.Api.UI.Wpf.cmd      (패치 스크립트)
```

### SourceTree.Api.UI.Wpf.cmd (현재 적용 스크립트)
```powershell
$dll = 'C:\Users\[사용자명]\AppData\Local\SourceTree\app-3.4.23\SourceTree.Api.UI.Wpf.dll'
Copy-Item $dll "$dll.bak"
$b = [System.IO.File]::ReadAllBytes($dll)
# Patch 1: BAML ConverterParameter 90->00
$b[0x205778] = 0x30
# Patch 2: getter always returns true
$b[0x119A8] = 0x17  # ldc.i4.1
$b[0x119A9] = 0x2A  # ret
[System.IO.File]::WriteAllBytes($dll, $b)
Write-Host "Done."
```
> `[사용자명]`을 실제 Windows 사용자명으로 교체. SourceTree 3.4.23.0 전용.

---

## 현재 DLL 패치 상태 (2026-03-27 기준)

```
Patch1 (0x205778): 0x30 ✅ 적용됨 (was 0x39)
Patch2 (0x119A8):  0x17 0x2A ✅ 적용됨 (was 0x00 0x02)
getter code: 17 2A 7B B2 01 00 04 0A 2B 00 06 2A
BoolToWidthConverter code: 00 05 2D 03 14 2B 06 05 6F 00 01 00 0A 12 00 28 BB 0A 00 0A
                           26 03 A5 8C 02 00 01 2D 0B 23 00 00 00 00 00 00 00 00 2B 01
                           06 8C 16 02 00 01 0B 2B 00 07 2A (미패치 상태)
```

---

## 로컬라이제이션 참고 (SourceTree.Localisation.dll)

```
ShowLogModifiedFilesColumnText = "Show Log Modified Files Column
                                   (but be aware this may increase the time taken to load the Log rows)"
ModifiedFiles = "Modified files"
```
→ UI 토글이 원래 존재했을 가능성이 있으나 현재 버전에서는 제거됨.
→ 성능에도 영향: 컬럼 계산 자체를 막으면 성능 향상 가능성 있음.
