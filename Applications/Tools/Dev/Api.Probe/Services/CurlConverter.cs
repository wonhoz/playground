using System.Text;
using ApiProbe.Models;

namespace ApiProbe.Services;

public static class CurlConverter
{
    public static string Convert(ApiRequest req)
    {
        var sb = new StringBuilder();
        sb.Append($"curl -X {req.Method} \"{req.Url}\"");

        foreach (var h in req.Headers)
        {
            if (h.Enabled && !string.IsNullOrWhiteSpace(h.Key))
                sb.Append($" \\\n  -H \"{h.Key}: {h.Value}\"");
        }

        if (!string.IsNullOrWhiteSpace(req.Body) &&
            req.Method is "POST" or "PUT" or "PATCH")
        {
            var escaped = req.Body.Replace("\"", "\\\"").Replace("\n", "\\n");
            sb.Append($" \\\n  -H \"Content-Type: {req.ContentType}\"");
            sb.Append($" \\\n  -d \"{escaped}\"");
        }

        return sb.ToString();
    }
}
