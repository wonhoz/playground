# Table.Craft — CSV 경량 GUI 분석기 신규 개발

- **날짜**: 2026-03-03
- **태그**: feature
- **상태**: 완료

---

## 개요

CSV/TSV 파일을 빠르게 열어 필터·정렬·집계·피벗 분석할 수 있는 경량 WPF 도구.
개발자·데이터 담당자 대상 Excel 대안.

---

## 구현 항목

### 인프라 (Task #8)
| 파일 | 설명 |
|------|------|
| `Table.Craft.csproj` | net10.0-windows, WPF 전용, 외부 NuGet 없음 |
| `GlobalUsings.cs` | WPF 충돌 방지 별칭 |
| `Models/ColumnDef.cs` | 컬럼 정의 + SortState + FilterChanged 이벤트 |
| `Models/FilterCondition.cs` | 필터 조건 모델 (12개 연산자) |
| `Models/AggregateResult.cs` | 집계 결과 모델 |
| `Models/PivotConfig.cs` | 피벗 설정 모델 |
| `Services/CsvParser.cs` | RFC 4180 CSV 파서, 구분자/인코딩 자동 감지, 타입 추론, 최근 파일 |
| `Services/QueryEngine.cs` | 필터·정렬·집계·피벗 인메모리 쿼리 엔진 |
| `Services/ExpressionEvaluator.cs` | 수식 평가기 (=A+B, IF, UPPER, LEN 등) |
| `Services/ExportService.cs` | CSV/TSV RFC 4180 내보내기 |

### UI (Task #9)
| 파일 | 설명 |
|------|------|
| `App.xaml` | 다크 테마 (Catppuccin Mocha 계열, 초록 액센트 #A6E3A1) |
| `App.xaml.cs` | Application 엔트리 |
| `MainWindow.xaml` | 툴바 + DataGrid + 집계 패널 + 상태바 |
| `MainWindow.xaml.cs` | FilteredList (가상 스크롤) + 전체 UI 로직 |
| `Views/FilterDialog.xaml/cs` | 고급 필터 설정 다이얼로그 |
| `Views/CalcColDialog.xaml/cs` | 계산 컬럼 추가 다이얼로그 |
| `Views/PivotView.xaml/cs` | 피벗 테이블 뷰 |

### 등록 (Task #10)
- `gen-icon.ps1` + `Resources/app.ico` 생성
- `Playground.slnx` 등록
- `+publish.cmd` 메뉴 #41, 선택, PUBALL 섹션 추가
- `+publish-all.cmd` 항목 추가

---

## 핵심 기술 결정

### 가상 스크롤 (100만 행+ 대응)
```csharp
sealed class FilteredList : IList<string[]>, IList, INotifyCollectionChanged
{
    // int[] _idx → string[][] _src 인덱스 매핑
    // NotifyCollectionChangedAction.Reset 으로 DataGrid 갱신
}
```
- WPF DataGrid의 `VirtualizingPanel.VirtualizationMode="Recycling"` 으로 보이는 행만 렌더링
- `FilteredList.Update(src, idx)` 호출 시 DataGrid 전체 갱신

### 컬럼 헤더에 필터 TextBox 내장
```xml
<DataTemplate x:Key="ColHdrTemplate">
  <Button Tag="{Binding}" Click="SortBtn_Click"/>  <!-- 정렬 클릭 -->
  <TextBox Text="{Binding FilterText, UpdateSourceTrigger=PropertyChanged}"/>  <!-- 빠른 필터 -->
</DataTemplate>
```
- `ColumnDef.FilterChanged` 이벤트 → `QueryEngine.SetQuickFilter()` → `ApplyQueryAsync()`
- `CanUserSortColumns="False"` + 수동 Shift+클릭 다중 정렬

### 비동기 쿼리 (디바운스)
```csharp
_queryCts?.Cancel();
_queryCts = new CancellationTokenSource();
var indices = await Task.Run(() => _engine.Compute(ct), ct);
```

---

## 빌드 결과
```
경고 0개, 오류 0개 ✅
```

---

## 키보드 단축키
| 단축키 | 기능 |
|--------|------|
| Ctrl+O | 파일 열기 |
| Ctrl+F | 고급 필터 |
| Ctrl+E | 내보내기 |
| Shift+컬럼 클릭 | 다중 정렬 |
| 드래그 앤 드롭 | 파일 열기 |
