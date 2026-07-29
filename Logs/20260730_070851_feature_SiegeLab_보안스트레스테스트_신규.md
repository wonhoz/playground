# Siege.Lab 신규 구현 — 보안 · 스트레스 종합 테스트

- **일시**: 2026-07-30 07:08 (KST)
- **태그**: feature
- **컴포넌트**: siege.lab
- **버전**: v1.0.0
- **경로**: `Applications/Network/Security/Siege.Lab`
- **요청**: 사용자 직접 요청 — i-pacs.net(소유·운영 대상)에 대한 보안 및 스트레스 테스트 도구

## 배경 / 권한
- 대상 `i-pacs.net`은 사용자가 **직접 소유·운영**하는 도메인임을 확인(질의 응답).
- 외부 실서비스(PACS 의료영상 추정)를 상대하므로, 소유자라도 오조작 방지를 위한 **안전장치를 기본 내장**.

## 안전 설계 (SafetyGate)
- **허용 목록(allowlist)**: 소유·허가 서약이 체크된 호스트만 능동 테스트 허용. 루프백/사설 대역은 항상 허용.
  - 저장: `%LocalAppData%\Playground\Siege.Lab\targets.json`
- **하드 상한**: 동시성 ≤ 500, RPS ≤ 2000, 지속 ≤ 3600초로 클램프.
- 모든 능동 테스트(부하·스캔·로그인·헬스)는 `Validate()` 게이트 통과 후에만 실행.
- 공격적 옵션(기본 자격증명 확인)은 별도 확인 다이얼로그.

## 기능 (6개 탭)
1. **대상**: 프로파일(이름·URL·인증헤더·허가서약) 등록/삭제, 허용 목록 관리.
2. **부하/스트레스** (`LoadTestEngine`): 동시 사용자·지속·램프업·최대 RPS·타임아웃 설정.
   실시간 집계 — 총 요청/성공/실패, 현재·평균 RPS, 지연 min/avg/p50/p90/p95/p99/max, 처리량(MB/s),
   활성 워커, 상태코드·오류 분포. SocketsHttpHandler 커넥션 풀 + 토큰 페이싱 레이트리미터.
3. **보안 점검** (`SecurityAuditor`, 비침습): TLS 프로토콜·인증서 만료·약한 서명, 보안 헤더(HSTS·CSP·
   X-Content-Type-Options·X-Frame-Options·Referrer-Policy·Permissions-Policy), 쿠키 플래그(Secure·
   HttpOnly·SameSite), 정보 노출(Server·X-Powered-By 등), CORS(와일드카드·오리진 반사), OPTIONS 허용 메서드,
   HTTPS→HTTP 다운그레이드 리다이렉트. 0~100 점수 + A~F 등급.
4. **가용성** (`HealthMonitor`): 주기 헬스체크, 가동률%·연속 실패·최근/평균 지연·이력 로그.
5. **취약점 · 노출** (`VulnScanner`, 능동/정찰 등급, 요청 간 지연): 민감 파일(.git·.env·백업·덤프 등),
   관리·디버그 콘솔(phpMyAdmin·actuator·swagger 등), **웹셸/백도어 흔적**(c99·r57·wso·shell.php 등),
   디렉터리 리스팅, 상세 오류/스택 트레이스 노출, TRACE(XST), (공격적) HTTP Basic 기본 자격증명.
6. **로그인 방어** (`LoginResilienceTester`): 의도적으로 **틀린 자격증명**을 반복 전송하여 레이트리밋·계정
   잠금·429·타르핏 등 무차별 대입 방어가 작동하는지 검증. 기본 사용자명은 미존재 계정으로 실계정 잠금 회피.
   판정 — 방어 감지(차단 시점·사유)/미감지/의심스러운 성공.

- **리포트** (`ReportExporter`): 보안 발견 + 부하 결과 Markdown 저장.

## 사용자 추가 요청 반영
- "백도어 같은 구멍을 찾는 테스트" → VulnScanner의 **노출·백도어 그룹**(웹셸 파일명·노출 파일·인증 없는 콘솔).
- "계속적인 로그인 시도 테스트" → **로그인 방어 탭**(brute-force 저항성) 신설.

## 기술
- .NET 10 / WPF, 다크 테마(App.xaml — 검증된 ControlTemplate 세트 재사용, 액센트 시안 #22D3EE).
- DwmSetWindowAttribute 다크 타이틀바. 단일 파일 배포 설정.
- 아이콘: 방패+번개(보안+스트레스) 멀티사이즈 ICO (`gen_icon.ps1`).

## 등록 / 검증
- `Playground.slnx` → `/Applications/Network/Security/` 폴더 추가.
- `+publish.cmd`(N=109) · `+publish-selector.ps1`(N=109) · `+publish-all.cmd`(Network/Security 구역) 반영.
- `ideas/avoid/apps.md`에 `Siege.Lab` ✅ 구현 완료 등록.
- 빌드: `dotnet build Siege.Lab.csproj -c Release` → 오류 0 / 경고 0.
- (솔루션 전체 빌드의 기존 C++ vcxproj 2건 오류는 VS MSBuild 요구 사항으로 본 작업과 무관.)

## 향후 보완 여지
- 부하 결과 실시간 라인 차트, 발견 항목 스트리밍 표시, 다중 엔드포인트 동시 헬스체크, HTML 리포트.
