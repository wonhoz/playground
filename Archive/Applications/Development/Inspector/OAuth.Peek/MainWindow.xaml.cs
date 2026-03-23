using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace OAuthPeek;

public partial class MainWindow : Window
{
    // ── 상수 ─────────────────────────────────────────────────────────────────
    private static readonly string HistoryPath =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "OAuthPeek", "history.json");

    private const int MaxHistory = 10;

    // ── 필드 ─────────────────────────────────────────────────────────────────
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(15) };
    private readonly DispatcherTimer _expTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    private DateTime? _tokenExpiry;
    private List<HistoryItem> _history = [];

    // ── Win32 다크 타이틀바 ───────────────────────────────────────────────────
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    // ── RFC 7519 클레임 설명 ──────────────────────────────────────────────────
    private static readonly Dictionary<string, string> ClaimDescriptions = new()
    {
        ["iss"] = "Issuer — 토큰 발급자",
        ["sub"] = "Subject — 토큰 주체(사용자 ID)",
        ["aud"] = "Audience — 수신 대상",
        ["exp"] = "Expiration Time — 만료 시각",
        ["nbf"] = "Not Before — 유효 시작 시각",
        ["iat"] = "Issued At — 발급 시각",
        ["jti"] = "JWT ID — 고유 식별자",
        ["name"] = "Full Name — 전체 이름",
        ["given_name"] = "Given Name — 이름",
        ["family_name"] = "Family Name — 성",
        ["email"] = "Email — 이메일 주소",
        ["email_verified"] = "Email Verified — 이메일 인증 여부",
        ["phone_number"] = "Phone Number — 전화번호",
        ["address"] = "Address — 주소",
        ["birthdate"] = "Birthdate — 생년월일",
        ["locale"] = "Locale — 지역/언어",
        ["zoneinfo"] = "Zone Info — 시간대",
        ["updated_at"] = "Updated At — 정보 업데이트 시각",
        ["nonce"] = "Nonce — 재전송 방지 값",
        ["acr"] = "Authentication Context Class — 인증 수준",
        ["amr"] = "Authentication Methods — 인증 방법",
        ["azp"] = "Authorized Party — 승인된 클라이언트",
        ["at_hash"] = "Access Token Hash — 액세스 토큰 해시",
        ["c_hash"] = "Code Hash — 인증 코드 해시",
        ["sid"] = "Session ID — 세션 식별자",
        ["roles"] = "Roles — 역할",
        ["scope"] = "Scope — 권한 범위",
        ["client_id"] = "Client ID — 클라이언트 식별자",
        ["tid"] = "Tenant ID — 테넌트 식별자 (Azure)",
        ["oid"] = "Object ID — 개체 식별자 (Azure)",
        ["upn"] = "User Principal Name — 사용자 주체 이름 (Azure)",
    };

    // ── 생성자 ────────────────────────────────────────────────────────────────
    public MainWindow()
    {
        InitializeComponent();
        _expTimer.Tick += ExpTimer_Tick;
    }

    // ── Window_Loaded ─────────────────────────────────────────────────────────
    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        // 다크 타이틀바
        var hwnd = new System.Windows.Interop.WindowInteropHelper(this).Handle;
        int darkMode = 1;
        DwmSetWindowAttribute(hwnd, 20, ref darkMode, sizeof(int));

        LoadHistory();
        DrawFlow(0); // Authorization Code 초기 렌더링
        SetStatus("준비 완료 — JWT 토큰, Issuer URL, Base64URL 문자열을 붙여넣고 분석하세요.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 입력 자동 감지 & 분석
    // ═══════════════════════════════════════════════════════════════════════════

    private void TxtInput_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        var text = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        if (IsJwt(text))
            SetStatus("JWT 감지됨 — '🔍 분석' 버튼을 누르거나 JWK URI를 입력하세요.");
        else if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            SetStatus("URL 감지됨 — OIDC 탭에서 Discovery 조회를 시도해 보세요.");
        else
            SetStatus("텍스트 입력됨 — Base64URL 탭에서 디코딩을 시도해 볼 수 있습니다.");
    }

    private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtInput.Text.Trim();
        if (string.IsNullOrEmpty(text)) { SetStatus("입력이 없습니다."); return; }

        if (IsJwt(text))
        {
            DecodeJwt(text);
            ResultTabs.SelectedIndex = 0;
            AddHistory(text);
        }
        else if (text.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            TxtIssuerUrl.Text = text;
            ResultTabs.SelectedIndex = 1;
            SetStatus("OIDC 탭으로 이동했습니다. Discovery 조회 버튼을 눌러주세요.");
        }
        else
        {
            TxtB64Input.Text = text;
            ResultTabs.SelectedIndex = 4;
            SetStatus("Base64URL 탭으로 이동했습니다.");
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        TxtInput.Clear();
        GridHeader.ItemsSource = null;
        GridClaims.ItemsSource = null;
        LblJwtAlg.Text = "알고리즘: —";
        LblJwtExp.Text = "만료: —";
        LblJwtValid.Text = "서명: 미확인";
        LblJwtValid.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));
        LblVerifyResult.Text = "";
        _expTimer.Stop();
        _tokenExpiry = null;
        SetStatus("지워졌습니다.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // JWT 디코딩
    // ═══════════════════════════════════════════════════════════════════════════

    private void DecodeJwt(string raw)
    {
        try
        {
            var handler = new JsonWebTokenHandler();
            var token = handler.ReadJsonWebToken(raw);

            // 헤더 클레임 (Base64URL 직접 파싱)
            var headerPart = raw.Split('.')[0];
            var headerJson = Encoding.UTF8.GetString(Base64UrlDecode(headerPart));
            using var headerDoc = JsonDocument.Parse(headerJson);
            var headerItems = headerDoc.RootElement.EnumerateObject()
                .Select(p => new ClaimRow { Key = p.Name, Value = p.Value.ToString(), Description = "" })
                .ToList();
            GridHeader.ItemsSource = headerItems;

            // 페이로드 클레임
            var payloadItems = token.Claims
                .Select(c => new ClaimRow
                {
                    Key = c.Type,
                    Value = FormatClaimValue(c.Type, c.Value),
                    Description = ClaimDescriptions.GetValueOrDefault(c.Type, "")
                })
                .ToList();
            GridClaims.ItemsSource = payloadItems;

            // 알고리즘
            var alg = token.Alg ?? "Unknown";
            LblJwtAlg.Text = $"알고리즘: {alg}";

            // 만료
            _expTimer.Stop();
            _tokenExpiry = token.ValidTo == DateTime.MinValue ? null : token.ValidTo.ToLocalTime();
            if (_tokenExpiry.HasValue)
            {
                UpdateExpLabel();
                _expTimer.Start();
            }
            else
            {
                LblJwtExp.Text = "만료: 없음 (nbf/exp 미설정)";
                LblJwtExp.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC));
            }

            LblJwtValid.Text = "서명: 미확인 (JWK URI 필요)";
            LblJwtValid.Foreground = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88));

            // JWK URI 자동 추론
            if (string.IsNullOrEmpty(TxtJwkUri.Text))
            {
                var issClaim = token.Claims.FirstOrDefault(c => c.Type == "iss");
                if (issClaim?.Value is { } iss && iss.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    TxtJwkUri.Text = iss.TrimEnd('/') + "/.well-known/jwks.json";
            }

            SetStatus($"JWT 디코딩 완료 — {alg}, 클레임 {payloadItems.Count}개");
        }
        catch (Exception ex)
        {
            SetStatus($"JWT 파싱 오류: {ex.Message}");
            MessageBox.Show($"JWT 파싱 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void ExpTimer_Tick(object? sender, EventArgs e)
    {
        if (!_tokenExpiry.HasValue) { _expTimer.Stop(); return; }
        UpdateExpLabel();
    }

    private void UpdateExpLabel()
    {
        if (!_tokenExpiry.HasValue) return;
        var remaining = _tokenExpiry.Value - DateTime.Now;
        if (remaining.TotalSeconds > 0)
        {
            LblJwtExp.Text = $"만료: {remaining.Days}일 {remaining.Hours:D2}:{remaining.Minutes:D2}:{remaining.Seconds:D2} 남음";
            LblJwtExp.Foreground = remaining.TotalMinutes < 5
                ? new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x40))
                : new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80));
        }
        else
        {
            LblJwtExp.Text = $"만료: 만료됨 ({_tokenExpiry.Value:yyyy-MM-dd HH:mm:ss})";
            LblJwtExp.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            _expTimer.Stop();
        }
    }

    // ── JWK 서명 검증 ─────────────────────────────────────────────────────────
    private async void BtnVerifyJwk_Click(object sender, RoutedEventArgs e)
    {
        var raw = TxtInput.Text.Trim();
        var jwkUri = TxtJwkUri.Text.Trim();

        if (!IsJwt(raw)) { SetStatus("JWT 토큰을 먼저 입력하세요."); return; }
        if (string.IsNullOrEmpty(jwkUri)) { SetStatus("JWK URI를 입력하세요."); return; }

        BtnVerifyJwk.IsEnabled = false;
        LblVerifyResult.Text = "검증 중...";
        LblVerifyResult.Foreground = new SolidColorBrush(Color.FromRgb(0xAA, 0xAA, 0xCC));

        try
        {
            var json = await _http.GetStringAsync(jwkUri);
            var jwks = new JsonWebKeySet(json);

            var handler = new JsonWebTokenHandler();
            var jwtToken = handler.ReadJsonWebToken(raw);
            var issClaim = jwtToken.Claims.FirstOrDefault(c => c.Type == "iss")?.Value ?? "";

            var validationParams = new TokenValidationParameters
            {
                IssuerSigningKeys = jwks.GetSigningKeys(),
                ValidateAudience = false,
                ValidateIssuer = !string.IsNullOrEmpty(issClaim),
                ValidIssuer = issClaim,
                ValidateLifetime = false,
            };

            var result = await handler.ValidateTokenAsync(raw, validationParams);

            if (result.IsValid)
            {
                LblJwtValid.Text = "서명: ✅ 유효";
                LblJwtValid.Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80));
                LblVerifyResult.Text = "✅ 서명 검증 성공";
                LblVerifyResult.Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80));
                SetStatus("JWK 서명 검증 성공");
            }
            else
            {
                LblJwtValid.Text = "서명: ❌ 실패";
                LblJwtValid.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
                LblVerifyResult.Text = $"❌ 검증 실패: {result.Exception?.Message}";
                LblVerifyResult.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
                SetStatus("JWK 서명 검증 실패");
            }
        }
        catch (Exception ex)
        {
            LblVerifyResult.Text = $"오류: {ex.Message}";
            LblVerifyResult.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x80, 0x40));
            SetStatus($"JWK 조회 오류: {ex.Message}");
        }
        finally
        {
            BtnVerifyJwk.IsEnabled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OIDC Discovery
    // ═══════════════════════════════════════════════════════════════════════════

    private async void BtnFetchOidc_Click(object sender, RoutedEventArgs e)
    {
        var issuer = TxtIssuerUrl.Text.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(issuer)) { SetStatus("Issuer URL을 입력하세요."); return; }

        BtnFetchOidc.IsEnabled = false;
        LblOidcStatus.Text = "조회 중...";
        GridOidc.ItemsSource = null;

        try
        {
            var discoveryUrl = issuer.Contains("/.well-known/") ? issuer
                : issuer + "/.well-known/openid-configuration";

            var json = await _http.GetStringAsync(discoveryUrl);
            using var doc = JsonDocument.Parse(json);

            var rows = doc.RootElement.EnumerateObject()
                .Select(p => new ClaimRow
                {
                    Key = p.Name,
                    Value = p.Value.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", p.Value.EnumerateArray().Select(x => x.ToString()))
                        : p.Value.ToString(),
                    Description = ""
                })
                .ToList();

            GridOidc.ItemsSource = rows;
            LblOidcStatus.Text = $"✅ {rows.Count}개 항목";
            LblOidcStatus.Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0xE0, 0x80));

            // JWK URI 자동 채우기
            var jwksUri = doc.RootElement.TryGetProperty("jwks_uri", out var v) ? v.GetString() : null;
            if (!string.IsNullOrEmpty(jwksUri) && string.IsNullOrEmpty(TxtJwkUri.Text))
                TxtJwkUri.Text = jwksUri;

            SetStatus($"OIDC Discovery 완료 — {rows.Count}개 항목");
        }
        catch (Exception ex)
        {
            LblOidcStatus.Text = $"❌ 오류";
            LblOidcStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0x50, 0x50));
            SetStatus($"OIDC Discovery 오류: {ex.Message}");
            MessageBox.Show($"Discovery 조회 실패:\n{ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnFetchOidc.IsEnabled = true;
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // OAuth2 Flow 다이어그램
    // ═══════════════════════════════════════════════════════════════════════════

    private void CmbFlow_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        DrawFlow(CmbFlow.SelectedIndex);
    }

    private void DrawFlow(int flowIndex)
    {
        FlowCanvas.Children.Clear();

        var flows = new Action<Canvas>[]
        {
            DrawAuthorizationCode,
            DrawAuthorizationCodePkce,
            DrawClientCredentials,
            DrawDeviceCode,
            DrawImplicit,
            DrawResourceOwnerPassword,
        };

        if (flowIndex >= 0 && flowIndex < flows.Length)
            flows[flowIndex](FlowCanvas);
    }

    // ── 참여자 박스 + 화살표 헬퍼 ─────────────────────────────────────────────

    private static TextBlock AddActor(Canvas canvas, double x, double centerY, string label, Color color)
    {
        var rect = new Rectangle
        {
            Width = 120, Height = 36,
            Fill = new SolidColorBrush(Color.FromArgb(0x30, color.R, color.G, color.B)),
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 1.5,
            RadiusX = 6, RadiusY = 6
        };
        Canvas.SetLeft(rect, x - 60);
        Canvas.SetTop(rect, centerY - 18);
        canvas.Children.Add(rect);

        var tb = new TextBlock
        {
            Text = label, Foreground = new SolidColorBrush(color),
            FontSize = 11, FontWeight = FontWeights.SemiBold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        Canvas.SetLeft(tb, x - tb.DesiredSize.Width / 2);
        Canvas.SetTop(tb, centerY - tb.DesiredSize.Height / 2);
        canvas.Children.Add(tb);

        return tb;
    }

    private static void AddArrow(Canvas canvas, double x1, double y1, double x2, double y2,
        string label, Color color, bool dashed = false)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = new SolidColorBrush(color), StrokeThickness = 1.5
        };
        if (dashed) line.StrokeDashArray = [4, 3];
        canvas.Children.Add(line);

        // 화살촉
        bool goRight = x2 > x1;
        double arrowLen = 10, arrowW = 5;
        double ax = goRight ? x2 - arrowLen : x2 + arrowLen;
        canvas.Children.Add(new Polygon
        {
            Points = goRight
                ? [new(x2, y2), new(ax, y2 - arrowW), new(ax, y2 + arrowW)]
                : [new(x2, y2), new(ax, y2 - arrowW), new(ax, y2 + arrowW)],
            Fill = new SolidColorBrush(color)
        });

        // 레이블
        if (!string.IsNullOrEmpty(label))
        {
            var tb = new TextBlock
            {
                Text = label, Foreground = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xDD)),
                FontSize = 10
            };
            tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Canvas.SetLeft(tb, (x1 + x2) / 2 - tb.DesiredSize.Width / 2);
            Canvas.SetTop(tb, Math.Min(y1, y2) - 16);
            canvas.Children.Add(tb);
        }
    }

    private static void AddStep(Canvas canvas, double x, double y, string text, Color color)
    {
        var tb = new TextBlock
        {
            Text = text, Foreground = new SolidColorBrush(color),
            FontSize = 10, MaxWidth = 200, TextWrapping = TextWrapping.Wrap
        };
        Canvas.SetLeft(tb, x);
        Canvas.SetTop(tb, y);
        canvas.Children.Add(tb);
    }

    // ── Flow 1: Authorization Code ────────────────────────────────────────────
    private static void DrawAuthorizationCode(Canvas c)
    {
        AddFlowTitle(c, "Authorization Code Flow");
        double user = 80, app = 300, auth = 520, res = 720;
        double ty = 60;

        var col1 = Color.FromRgb(0x50, 0xC8, 0xDC);
        var col2 = Color.FromRgb(0xA0, 0xE0, 0x80);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);
        var col4 = Color.FromRgb(0xC0, 0x80, 0xFF);

        AddActor(c, user, ty, "사용자", col1);
        AddActor(c, app, ty, "클라이언트 앱", col2);
        AddActor(c, auth, ty, "인가 서버", col3);
        AddActor(c, res, ty, "리소스 서버", col4);

        double y = 110;
        double step = 52;

        AddArrow(c, user, y, app, y, "① 로그인 요청", col1); y += step;
        AddArrow(c, app, y, auth, y, "② 인가 요청 (redirect_uri, scope)", col2); y += step;
        AddArrow(c, auth, y, user, y, "③ 로그인 UI 표시", col3, dashed: true); y += step;
        AddArrow(c, user, y, auth, y, "④ 자격증명 입력", col1); y += step;
        AddArrow(c, auth, y, app, y, "⑤ Authorization Code 반환", col3, dashed: true); y += step;
        AddArrow(c, app, y, auth, y, "⑥ Code + client_secret → Token 교환", col2); y += step;
        AddArrow(c, auth, y, app, y, "⑦ Access Token + Refresh Token", col3, dashed: true); y += step;
        AddArrow(c, app, y, res, y, "⑧ API 요청 (Bearer Token)", col2); y += step;
        AddArrow(c, res, y, app, y, "⑨ 보호된 리소스 반환", col4, dashed: true);
    }

    // ── Flow 2: Authorization Code + PKCE ────────────────────────────────────
    private static void DrawAuthorizationCodePkce(Canvas c)
    {
        AddFlowTitle(c, "Authorization Code + PKCE Flow");
        double user = 80, app = 300, auth = 520, res = 720;
        double ty = 60;

        var col1 = Color.FromRgb(0x50, 0xC8, 0xDC);
        var col2 = Color.FromRgb(0xA0, 0xE0, 0x80);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);
        var col4 = Color.FromRgb(0xC0, 0x80, 0xFF);

        AddActor(c, user, ty, "사용자", col1);
        AddActor(c, app, ty, "클라이언트 앱\n(공개 클라이언트)", col2);
        AddActor(c, auth, ty, "인가 서버", col3);
        AddActor(c, res, ty, "리소스 서버", col4);

        double y = 110; double step = 50;

        AddStep(c, app - 60, y - 14, "code_verifier 생성\ncode_challenge = S256(verifier)", col2);
        y += step;
        AddArrow(c, app, y, auth, y, "② 인가 요청 + code_challenge", col2); y += step;
        AddArrow(c, auth, y, user, y, "③ 로그인 UI 표시", col3, dashed: true); y += step;
        AddArrow(c, user, y, auth, y, "④ 자격증명 입력", col1); y += step;
        AddArrow(c, auth, y, app, y, "⑤ Authorization Code 반환", col3, dashed: true); y += step;
        AddArrow(c, app, y, auth, y, "⑥ Code + code_verifier → Token 교환", col2); y += step;
        AddStep(c, auth - 60, y - 14, "code_challenge 검증\n(서버 시크릿 불필요)", col3); y += step;
        AddArrow(c, auth, y, app, y, "⑦ Access Token + Refresh Token", col3, dashed: true); y += step;
        AddArrow(c, app, y, res, y, "⑧ API 요청 (Bearer Token)", col2); y += step;
        AddArrow(c, res, y, app, y, "⑨ 보호된 리소스 반환", col4, dashed: true);
    }

    // ── Flow 3: Client Credentials ────────────────────────────────────────────
    private static void DrawClientCredentials(Canvas c)
    {
        AddFlowTitle(c, "Client Credentials Flow (M2M)");
        double app = 200, auth = 500, res = 700;
        double ty = 60;

        var col2 = Color.FromRgb(0xA0, 0xE0, 0x80);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);
        var col4 = Color.FromRgb(0xC0, 0x80, 0xFF);

        AddActor(c, app, ty, "서버 / 마이크로서비스", col2);
        AddActor(c, auth, ty, "인가 서버", col3);
        AddActor(c, res, ty, "리소스 서버", col4);

        double y = 120; double step = 70;

        AddStep(c, 10, y - 14, "사용자 개입 없음 — 앱 자체 인증", Color.FromRgb(0x88, 0x88, 0xAA));
        y += 30;
        AddArrow(c, app, y, auth, y, "① client_id + client_secret → Token 요청", col2); y += step;
        AddArrow(c, auth, y, app, y, "② Access Token (사용자 스코프 없음)", col3, dashed: true); y += step;
        AddArrow(c, app, y, res, y, "③ API 요청 (Bearer Token)", col2); y += step;
        AddArrow(c, res, y, app, y, "④ 보호된 리소스 반환", col4, dashed: true);
    }

    // ── Flow 4: Device Code ───────────────────────────────────────────────────
    private static void DrawDeviceCode(Canvas c)
    {
        AddFlowTitle(c, "Device Code Flow (TV / IoT)");
        double dev = 80, auth = 380, user = 620;
        double ty = 60;

        var col1 = Color.FromRgb(0x50, 0xC8, 0xDC);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);
        var col5 = Color.FromRgb(0xFF, 0x90, 0x90);

        AddActor(c, dev, ty, "기기 (TV/IoT)", col1);
        AddActor(c, auth, ty, "인가 서버", col3);
        AddActor(c, user, ty, "사용자 (스마트폰)", col5);

        double y = 110; double step = 55;

        AddArrow(c, dev, y, auth, y, "① device_code 요청 (client_id)", col1); y += step;
        AddArrow(c, auth, y, dev, y, "② device_code + user_code + URL 반환", col3, dashed: true); y += step;
        AddStep(c, dev - 60, y - 14, "화면에 user_code 표시\n(예: ABCD-1234)", col1); y += 30;
        AddArrow(c, dev, y, auth, y, "③ 폴링: device_code로 Token 조회", col1); y += step;
        AddArrow(c, auth, y, dev, y, "→ authorization_pending 응답", col3, dashed: true); y += step / 2;
        AddArrow(c, user, y, auth, y, "④ URL 접속 + user_code 입력 + 승인", col5); y += step;
        AddArrow(c, auth, y, dev, y, "⑤ 폴링 응답: Access Token!", col3, dashed: true);
    }

    // ── Flow 5: Implicit (Legacy) ─────────────────────────────────────────────
    private static void DrawImplicit(Canvas c)
    {
        AddFlowTitle(c, "Implicit Flow (Legacy — 권장하지 않음)");
        AddStep(c, 20, 80,
            "⚠️ Implicit Flow는 RFC 6749에서 정의됐으나 보안상 취약점으로 인해\n" +
            "   현재는 Authorization Code + PKCE 사용을 권장합니다.\n" +
            "   Access Token이 URL fragment(#)에 포함되어 브라우저 히스토리에 노출됩니다.",
            Color.FromRgb(0xFF, 0x90, 0x40));

        double user = 100, app = 350, auth = 600;
        double ty = 160;
        var col1 = Color.FromRgb(0x50, 0xC8, 0xDC);
        var col2 = Color.FromRgb(0xA0, 0xE0, 0x80);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);

        AddActor(c, user, ty, "사용자/브라우저", col1);
        AddActor(c, app, ty, "SPA / 클라이언트", col2);
        AddActor(c, auth, ty, "인가 서버", col3);

        double y = 210; double step = 55;
        AddArrow(c, user, y, auth, y, "① 인가 요청 (response_type=token)", col1); y += step;
        AddArrow(c, auth, y, user, y, "② 로그인 UI", col3, dashed: true); y += step;
        AddArrow(c, user, y, auth, y, "③ 자격증명 입력", col1); y += step;
        AddArrow(c, auth, y, user, y, "④ redirect_uri#access_token=... (URL fragment)", col3, dashed: true); y += step;
        AddArrow(c, user, y, app, y, "⑤ fragment에서 Token 추출", col1);
    }

    // ── Flow 6: Resource Owner Password ──────────────────────────────────────
    private static void DrawResourceOwnerPassword(Canvas c)
    {
        AddFlowTitle(c, "Resource Owner Password Credentials (Legacy)");
        AddStep(c, 20, 80,
            "⚠️ ROPC는 사용자 자격증명을 클라이언트가 직접 처리하므로 보안 위험이 높습니다.\n" +
            "   신뢰된 퍼스트파티 앱이나 레거시 마이그레이션에만 제한적으로 사용하세요.",
            Color.FromRgb(0xFF, 0x90, 0x40));

        double user = 100, app = 370, auth = 620;
        double ty = 160;
        var col1 = Color.FromRgb(0x50, 0xC8, 0xDC);
        var col2 = Color.FromRgb(0xA0, 0xE0, 0x80);
        var col3 = Color.FromRgb(0xFF, 0xC0, 0x50);

        AddActor(c, user, ty, "사용자", col1);
        AddActor(c, app, ty, "클라이언트 앱", col2);
        AddActor(c, auth, ty, "인가 서버", col3);

        double y = 210; double step = 60;
        AddArrow(c, user, y, app, y, "① username + password 직접 전달", col1); y += step;
        AddArrow(c, app, y, auth, y, "② grant_type=password + 자격증명", col2); y += step;
        AddArrow(c, auth, y, app, y, "③ Access Token + Refresh Token", col3, dashed: true); y += step;
        AddStep(c, app - 60, y - 14, "클라이언트가 사용자 비밀번호를\n직접 처리 — 신뢰 필수", Color.FromRgb(0xFF, 0x90, 0x40));
    }

    private static void AddFlowTitle(Canvas c, string title)
    {
        var tb = new TextBlock
        {
            Text = title,
            Foreground = new SolidColorBrush(Color.FromRgb(0x50, 0xC8, 0xDC)),
            FontSize = 13, FontWeight = FontWeights.Bold
        };
        Canvas.SetLeft(tb, 20);
        Canvas.SetTop(tb, 14);
        c.Children.Add(tb);
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Auth Header 생성기
    // ═══════════════════════════════════════════════════════════════════════════

    private void BtnCopyBearer_Click(object sender, RoutedEventArgs e)
    {
        var token = TxtBearerToken.Text.Trim();
        if (string.IsNullOrEmpty(token)) { SetStatus("Bearer 토큰을 입력하세요."); return; }

        var header = $"Authorization: Bearer {token}";
        LblBearerResult.Text = header;
        CopyToClipboard(header);
        SetStatus("Bearer 헤더가 클립보드에 복사됐습니다.");
    }

    private void BtnCopyBasic_Click(object sender, RoutedEventArgs e)
    {
        var user = TxtBasicUser.Text;
        var pass = TxtBasicPass.Text;
        if (string.IsNullOrEmpty(user)) { SetStatus("Username을 입력하세요."); return; }

        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        var header = $"Authorization: Basic {encoded}";
        LblBasicResult.Text = header;
        CopyToClipboard(header);
        SetStatus("Basic Auth 헤더가 클립보드에 복사됐습니다.");
    }

    private void BtnCopyApiKey_Click(object sender, RoutedEventArgs e)
    {
        var headerName = TxtApiKeyHeader.Text.Trim();
        var key = TxtApiKeyValue.Text.Trim();
        if (string.IsNullOrEmpty(headerName) || string.IsNullOrEmpty(key))
        {
            SetStatus("헤더 이름과 API Key를 입력하세요."); return;
        }

        var header = $"{headerName}: {key}";
        LblApiKeyResult.Text = header;
        CopyToClipboard(header);
        SetStatus("API Key 헤더가 클립보드에 복사됐습니다.");
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Base64URL 변환
    // ═══════════════════════════════════════════════════════════════════════════

    private void BtnB64Encode_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtB64Input.Text;
        if (string.IsNullOrEmpty(text)) { SetStatus("입력 텍스트가 없습니다."); return; }

        try
        {
            var bytes = Encoding.UTF8.GetBytes(text);
            TxtB64Output.Text = Base64UrlEncode(bytes);
            SetStatus("Base64URL 인코딩 완료");
        }
        catch (Exception ex)
        {
            SetStatus($"인코딩 오류: {ex.Message}");
        }
    }

    private void BtnB64Decode_Click(object sender, RoutedEventArgs e)
    {
        var text = TxtB64Input.Text.Trim();
        if (string.IsNullOrEmpty(text)) { SetStatus("입력 텍스트가 없습니다."); return; }

        try
        {
            var bytes = Base64UrlDecode(text);
            TxtB64Output.Text = Encoding.UTF8.GetString(bytes);
            SetStatus("Base64URL 디코딩 완료");
        }
        catch
        {
            // 표준 Base64로 재시도
            try
            {
                var bytes = Convert.FromBase64String(text);
                TxtB64Output.Text = Encoding.UTF8.GetString(bytes);
                SetStatus("Base64 디코딩 완료 (표준 형식)");
            }
            catch (Exception ex)
            {
                SetStatus($"디코딩 오류: {ex.Message}");
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 히스토리
    // ═══════════════════════════════════════════════════════════════════════════

    private void HistoryList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (HistoryList.SelectedItem is HistoryItem item)
        {
            TxtInput.Text = item.Raw;
            HistoryList.SelectedItem = null;
        }
    }

    private void BtnClearHistory_Click(object sender, RoutedEventArgs e)
    {
        _history.Clear();
        HistoryList.ItemsSource = null;
        SaveHistory();
        SetStatus("히스토리를 지웠습니다.");
    }

    private void AddHistory(string raw)
    {
        _history.RemoveAll(h => h.Raw == raw);
        _history.Insert(0, new HistoryItem
        {
            Raw = raw,
            Display = raw.Length > 40 ? raw[..37] + "..." : raw,
            AddedAt = DateTime.Now
        });
        if (_history.Count > MaxHistory)
            _history = _history[..MaxHistory];

        HistoryList.ItemsSource = null;
        HistoryList.ItemsSource = _history;
        SaveHistory();
    }

    private void LoadHistory()
    {
        try
        {
            if (!File.Exists(HistoryPath)) return;
            var json = File.ReadAllText(HistoryPath, Encoding.UTF8);
            _history = JsonSerializer.Deserialize<List<HistoryItem>>(json) ?? [];
            HistoryList.ItemsSource = _history;
        }
        catch { _history = []; }
    }

    private void SaveHistory()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(HistoryPath)!;
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
            var json = JsonSerializer.Serialize(_history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(HistoryPath, json, Encoding.UTF8);
        }
        catch { /* 저장 실패는 무시 */ }
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // 유틸리티
    // ═══════════════════════════════════════════════════════════════════════════

    private static bool IsJwt(string text)
    {
        var parts = text.Split('.');
        return parts.Length == 3 && parts.All(p => p.Length > 0);
    }

    private static string FormatValue(object? val) =>
        val?.ToString() ?? "";

    private static string FormatClaimValue(string claimType, string raw)
    {
        // Unix 타임스탬프 → 사람 읽을 수 있는 형식
        if (claimType is "exp" or "iat" or "nbf" or "auth_time" or "updated_at")
        {
            if (long.TryParse(raw, out var unix))
            {
                var dt = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
                return $"{raw}  ({dt:yyyy-MM-dd HH:mm:ss})";
            }
        }
        return raw;
    }

    private static string Base64UrlEncode(byte[] bytes)
    {
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static byte[] Base64UrlDecode(string s)
    {
        var base64 = s.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }

    private static void CopyToClipboard(string text)
    {
        try { Clipboard.SetText(text); }
        catch { /* 클립보드 접근 실패 무시 */ }
    }

    private void SetStatus(string msg)
    {
        StatusBar.Text = $"[{DateTime.Now:HH:mm:ss}] {msg}";
    }
}

// ── 데이터 모델 ───────────────────────────────────────────────────────────────

public record ClaimRow
{
    public string Key { get; init; } = "";
    public string Value { get; init; } = "";
    public string Description { get; init; } = "";
}

public class HistoryItem
{
    public string Raw { get; set; } = "";
    public string Display { get; set; } = "";
    public DateTime AddedAt { get; set; }
}
