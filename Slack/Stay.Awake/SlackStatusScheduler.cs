using System.Text.Json;

namespace StayAwake
{
    /// <summary>
    /// Slack presence 자동 변경 스케줄러
    /// - 아침 WorkStartHour시 → "auto" (활성)
    /// - 저녁 WorkEndHour시 → "away" (자리 비움)
    /// </summary>
    public class SlackStatusScheduler
    {
        private static readonly HttpClient _httpClient = new();
        private string? _token;
        private DateTime _lastActiveSet = DateTime.MinValue;
        private DateTime _lastAwaySet = DateTime.MinValue;

        public bool IsEnabled { get; set; }
        public int WorkStartHour { get; set; } = 9;
        public int WorkEndHour { get; set; } = 19;

        public bool HasToken => !string.IsNullOrWhiteSpace(_token);

        public void SetToken(string? token)
        {
            _token = token?.Trim();
        }

        /// <summary>
        /// 매 분 체크: 출근/퇴근 시간이면 Slack presence 자동 변경
        /// </summary>
        public async Task<SlackPresenceResult?> CheckAndSetPresenceAsync()
        {
            if (!IsEnabled || !HasToken) return null;

            var now = DateTime.Now;

            // 아침 WorkStartHour시 → 활성
            if (now.Hour == WorkStartHour && _lastActiveSet.Date != now.Date)
            {
                var result = await SetPresenceAsync("auto");
                if (result.Success) _lastActiveSet = now;
                return result;
            }

            // 저녁 WorkEndHour시 → 자리 비움
            if (now.Hour == WorkEndHour && _lastAwaySet.Date != now.Date)
            {
                var result = await SetPresenceAsync("away");
                if (result.Success) _lastAwaySet = now;
                return result;
            }

            return null;
        }

        public Task<SlackPresenceResult> SetActiveAsync() => SetPresenceAsync("auto");
        public Task<SlackPresenceResult> SetAwayAsync() => SetPresenceAsync("away");

        /// <summary>
        /// Slack users.setPresence API 호출
        /// </summary>
        /// <param name="presence">"auto" (활성) 또는 "away" (자리 비움)</param>
        public async Task<SlackPresenceResult> SetPresenceAsync(string presence)
        {
            if (!HasToken)
                return new SlackPresenceResult(presence, false, "토큰이 설정되지 않았습니다.");

            try
            {
                using var content = new FormUrlEncodedContent(
                [
                    new KeyValuePair<string, string>("token", _token!),
                    new KeyValuePair<string, string>("presence", presence)
                ]);

                var response = await _httpClient.PostAsync(
                    "https://slack.com/api/users.setPresence", content);
                var body = await response.Content.ReadAsStringAsync();

                using var doc = JsonDocument.Parse(body);
                var root = doc.RootElement;

                if (root.TryGetProperty("ok", out var ok) && ok.GetBoolean())
                    return new SlackPresenceResult(presence, true, null);

                // 에러 코드를 사람이 읽기 쉬운 메시지로 변환
                var errorCode = root.TryGetProperty("error", out var err) ? err.GetString() : "unknown";
                var errorMsg = errorCode switch
                {
                    "missing_scope"   => "토큰에 'users:write' 권한이 없습니다.\n앱 설정에서 User Token Scopes에 users:write를 추가하세요.",
                    "invalid_auth"    => "토큰이 유효하지 않습니다. 새 토큰을 발급하세요.",
                    "not_authed"      => "인증 정보가 없습니다. 토큰을 확인하세요.",
                    "token_revoked"   => "토큰이 폐기됐습니다. 새 토큰을 발급하세요.",
                    "account_inactive"=> "계정이 비활성 상태입니다.",
                    "no_permission"   => "권한이 없습니다. 워크스페이스 관리자에게 문의하세요.",
                    _                 => $"오류: {errorCode}"
                };

                return new SlackPresenceResult(presence, false, errorMsg);
            }
            catch (Exception ex)
            {
                return new SlackPresenceResult(presence, false, $"네트워크 오류: {ex.Message}");
            }
        }
    }

    public record SlackPresenceResult(string Presence, bool Success, string? Error);
}
