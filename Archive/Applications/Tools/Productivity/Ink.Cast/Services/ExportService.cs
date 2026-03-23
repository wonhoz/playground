using System.IO;
using System.Net;
using System.Text.RegularExpressions;
using InkCast.Models;
using Markdig;

namespace InkCast.Services;

/// <summary>노트 HTML 내보내기 및 미리보기 HTML 생성</summary>
public class ExportService
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .UseAdvancedExtensions()
        .UseEmojiAndSmiley()
        .Build();

    // CSS 상수 (다크 테마)
    private static string BuildCss(string? extraBody = null)
    {
        var bodyExtra = extraBody ?? "padding: 20px 28px;";
        return """
            :root { color-scheme: dark; }
            * { box-sizing: border-box; }
            body {
                background: #1e1e2e;
                color: #cdd6f4;
                font-family: -apple-system, 'Segoe UI', Roboto, 'Noto Sans KR', sans-serif;
                line-height: 1.75;
                max-width: 780px;
                margin: 0 auto;
                font-size: 15px;
            }
            h1,h2,h3,h4,h5,h6 { color: #cba6f7; margin: 1.4em 0 0.5em; }
            h1 { font-size: 1.9em; border-bottom: 1px solid #313244; padding-bottom: 0.3em; }
            h2 { font-size: 1.45em; border-bottom: 1px solid #313244; padding-bottom: 0.2em; }
            h3 { font-size: 1.2em; }
            p  { margin: 0.6em 0; }
            code {
                background: #313244; color: #a6e3a1;
                padding: 1px 5px; border-radius: 3px;
                font-family: 'Cascadia Code', 'Fira Code', Consolas, monospace;
                font-size: 0.875em;
            }
            pre {
                background: #181825; border: 1px solid #313244; border-radius: 6px;
                padding: 14px 16px; overflow-x: auto; margin: 1em 0;
            }
            pre code { background: transparent; padding: 0; color: #cdd6f4; font-size: 0.9em; }
            a { color: #89b4fa; text-decoration: none; }
            a:hover { text-decoration: underline; }
            .wiki-link {
                color: #cba6f7; border-bottom: 1px dashed #cba6f7;
                cursor: pointer; transition: color .15s;
            }
            .wiki-link:hover { color: #f5c2e7; }
            blockquote {
                border-left: 3px solid #6c7086;
                margin: 0.8em 0; padding: 0.2em 16px; color: #9399b2;
            }
            table { border-collapse: collapse; width: 100%; margin: 1em 0; }
            th, td { border: 1px solid #313244; padding: 7px 12px; text-align: left; }
            th { background: #313244; color: #cba6f7; }
            tr:nth-child(even) { background: #1a1a2e; }
            tr:hover { background: #242438; }
            ul, ol { padding-left: 1.5em; margin: 0.5em 0; }
            li { margin: 0.25em 0; }
            input[type='checkbox'] { accent-color: #a6e3a1; margin-right: 6px; }
            img { max-width: 100%; border-radius: 6px; }
            hr { border: none; border-top: 1px solid #313244; margin: 1.8em 0; }
            mark { background: #3d3250; color: #cdd6f4; border-radius: 2px; padding: 0 3px; }
            del { color: #6c7086; }
            """ + "\nbody { " + bodyExtra + " }";
    }

    /// <summary>노트를 미리보기 HTML로 변환 (WebView2용)</summary>
    public string ToPreviewHtml(string content)
    {
        var processed = PreprocessWikiLinks(content);
        var body      = Markdown.ToHtml(processed, Pipeline);
        var css       = BuildCss("padding: 14px 18px;");
        return
            "<!DOCTYPE html>\n<html lang=\"ko\">\n<head><meta charset=\"UTF-8\">" +
            "<style>\n" + css + "\n</style></head>\n" +
            "<body>" + body + "</body>\n</html>";
    }

    /// <summary>노트를 독립 실행 HTML 파일로 내보내기</summary>
    public async Task ExportToFileAsync(Note note, string filePath)
    {
        var processed = PreprocessWikiLinks(note.Content);
        var body      = Markdown.ToHtml(processed, Pipeline);
        var css       = BuildCss();
        var title     = WebUtility.HtmlEncode(note.Title);
        var footer    = $"내보낸 시각: {DateTime.Now:yyyy-MM-dd HH:mm} · Ink.Cast";
        var html =
            "<!DOCTYPE html>\n" +
            "<html lang=\"ko\">\n<head>\n" +
            "<meta charset=\"UTF-8\">\n" +
            "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\n" +
            $"<title>{title}</title>\n" +
            "<style>\n" + css + "\n</style>\n</head>\n<body>\n" +
            $"<h1>{title}</h1>\n" +
            body +
            "<footer style=\"margin-top:3em;padding-top:1em;border-top:1px solid #313244;" +
            "color:#585b70;font-size:0.8em;\">" +
            footer + "</footer>\n</body>\n</html>";
        await File.WriteAllTextAsync(filePath, html, System.Text.Encoding.UTF8);
    }

    private static string PreprocessWikiLinks(string content)
        => Regex.Replace(content, @"\[\[([^\[\]\n]+)\]\]",
            m => $"<span class=\"wiki-link\">{WebUtility.HtmlEncode(m.Groups[1].Value)}</span>");
}
