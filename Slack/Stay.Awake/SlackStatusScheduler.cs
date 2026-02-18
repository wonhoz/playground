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
                bool ok = await SetPresenceAsync("auto");
                if (ok) _lastActiveSet = now;
                return new SlackPresenceResult("auto", ok);
            }

            // 저녁 WorkEndHour시 → 자리 비움
            if (now.Hour == WorkEndHour && _lastAwaySet.Date != now.Date)
            {
                bool ok = await SetPresenceAsync("away");
                if (ok) _lastAwaySet = now;
                return new SlackPresenceResult("away", ok);
            }

            return null;
        }

        public async Task<bool> SetActiveAsync() => await SetPresenceAsync("auto");
        public async Task<bool> SetAwayAsync() => await SetPresenceAsync("away");

        /// <summary>
        /// Slack users.setPresence API 호출
        /// </summary>
        /// <param name="presence">"auto" (활성) 또는 "away" (자리 비움)</param>
        public async Task<bool> SetPresenceAsync(string presence)
        {
            if (!HasToken) return false;

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
                return body.Contains("\"ok\":true");
            }
            catch
            {
                return false;
            }
        }
    }

    public record SlackPresenceResult(string Presence, bool Success);
}
