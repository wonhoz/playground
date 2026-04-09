using System.Net.Http;
using System.Net.Http.Headers;

namespace Prompt.Forge.Services;

sealed class GistSync : IDisposable
{
    readonly HttpClient _http;

    public GistSync(string pat)
    {
        _http = new HttpClient();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", pat);
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("Prompt.Forge/1.0");
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>JSON 내용을 Gist에 업로드. gistId가 빈 문자열이면 새로 생성, 반환값은 Gist ID.</summary>
    public async Task<string> UploadAsync(string gistId, string jsonContent)
    {
        var filesPayload = new Dictionary<string, object>
        {
            ["prompts.json"] = new { content = jsonContent }
        };

        HttpResponseMessage resp;
        if (string.IsNullOrEmpty(gistId))
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                description = "Prompt.Forge Prompts Backup",
                @public = false,
                files = filesPayload
            });
            resp = await _http.PostAsync("https://api.github.com/gists",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        }
        else
        {
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                description = "Prompt.Forge Prompts Backup",
                files = filesPayload
            });
            resp = await _http.PatchAsync($"https://api.github.com/gists/{gistId}",
                new StringContent(body, System.Text.Encoding.UTF8, "application/json"));
        }

        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("id").GetString() ?? gistId;
    }

    /// <summary>Gist에서 prompts.json 내용을 다운로드.</summary>
    public async Task<string> DownloadAsync(string gistId)
    {
        var resp = await _http.GetAsync($"https://api.github.com/gists/{gistId}");
        resp.EnsureSuccessStatusCode();
        var result = await resp.Content.ReadAsStringAsync();
        using var doc = System.Text.Json.JsonDocument.Parse(result);
        return doc.RootElement
            .GetProperty("files")
            .GetProperty("prompts.json")
            .GetProperty("content")
            .GetString() ?? "[]";
    }

    public void Dispose() => _http.Dispose();
}
