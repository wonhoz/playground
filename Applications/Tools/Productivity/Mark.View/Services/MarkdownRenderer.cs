using Markdig;
using Markdig.Extensions.AutoIdentifiers;

namespace MarkView.Services;

public class MarkdownRenderer
{
    private readonly MarkdownPipeline _pipeline;

    public MarkdownRenderer()
    {
        _pipeline = new MarkdownPipelineBuilder()
            .UseAdvancedExtensions()
            .UseAutoIdentifiers(AutoIdentifierOptions.GitHub)
            .UseTaskLists()
            .UseGridTables()
            .UsePipeTables()
            .UseEmphasisExtras()
            .UseGenericAttributes()
            .Build();
    }

    public string RenderToHtml(string markdown, string? filePath = null)
    {
        var body = Markdown.ToHtml(markdown, _pipeline);
        return WrapInPage(body, filePath);
    }

    private string WrapInPage(string body, string? filePath)
    {
        var baseTag = filePath != null
            ? "<base href=\"file:///" + filePath.Replace("\\", "/").Replace(" ", "%20") + "\">"
            : "";

        return "<!DOCTYPE html><html><head><meta charset='utf-8'>" + baseTag + @"
<style>
* { box-sizing: border-box; margin: 0; padding: 0; }
:root {
  --bg: #1e1e2e;
  --surface: #2a2a3d;
  --border: #3a3a4d;
  --text: #cdd6f4;
  --text-dim: #888aa8;
  --accent: #89b4fa;
  --accent2: #cba6f7;
  --green: #a6e3a1;
  --red: #f38ba8;
  --yellow: #f9e2af;
  --orange: #fab387;
  --teal: #94e2d5;
  --code-bg: #181825;
  --blockquote-border: #89b4fa;
  --link: #89dceb;
}
html { background: var(--bg); color: var(--text); }
body {
  font-family: 'Segoe UI', 'Malgun Gothic', sans-serif;
  font-size: 15px;
  line-height: 1.75;
  max-width: 860px;
  margin: 0 auto;
  padding: 40px 32px 80px;
}
h1, h2, h3, h4, h5, h6 {
  color: var(--text);
  font-weight: 600;
  margin-top: 1.6em;
  margin-bottom: 0.6em;
  line-height: 1.3;
}
h1 { font-size: 2em; border-bottom: 2px solid var(--border); padding-bottom: 0.3em; color: var(--accent); }
h2 { font-size: 1.5em; border-bottom: 1px solid var(--border); padding-bottom: 0.2em; color: var(--accent2); }
h3 { font-size: 1.25em; color: var(--teal); }
h4 { font-size: 1.1em; color: var(--green); }
p { margin: 0.8em 0; }
a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }
code {
  background: var(--code-bg);
  color: var(--orange);
  padding: 0.1em 0.4em;
  border-radius: 4px;
  font-family: 'Cascadia Code', 'Consolas', monospace;
  font-size: 0.88em;
}
pre {
  background: var(--code-bg);
  border: 1px solid var(--border);
  border-radius: 8px;
  padding: 1.1em 1.3em;
  overflow-x: auto;
  margin: 1em 0;
}
pre code {
  background: none;
  color: var(--text);
  padding: 0;
  font-size: 0.9em;
}
blockquote {
  border-left: 4px solid var(--blockquote-border);
  background: var(--surface);
  margin: 1em 0;
  padding: 0.6em 1.2em;
  border-radius: 0 6px 6px 0;
  color: var(--text-dim);
}
blockquote p { margin: 0.3em 0; }
ul, ol { padding-left: 1.6em; margin: 0.6em 0; }
li { margin: 0.3em 0; }
li ul, li ol { margin: 0.2em 0; }
hr { border: none; border-top: 1px solid var(--border); margin: 2em 0; }
table {
  width: 100%;
  border-collapse: collapse;
  margin: 1em 0;
  font-size: 0.93em;
}
th {
  background: var(--surface);
  color: var(--accent);
  padding: 0.6em 1em;
  border: 1px solid var(--border);
  text-align: left;
  font-weight: 600;
}
td {
  padding: 0.5em 1em;
  border: 1px solid var(--border);
}
tr:nth-child(even) td { background: rgba(255,255,255,0.03); }
img {
  max-width: 100%;
  border-radius: 6px;
  margin: 0.5em 0;
}
input[type=checkbox] {
  accent-color: var(--accent);
  margin-right: 0.4em;
}
.task-list-item { list-style: none; margin-left: -1.2em; }
mark { background: rgba(249,226,175,0.2); color: var(--yellow); padding: 0 0.2em; border-radius: 2px; }
del { color: var(--text-dim); }
sup, sub { font-size: 0.75em; }
::-webkit-scrollbar { width: 8px; height: 8px; }
::-webkit-scrollbar-track { background: var(--bg); }
::-webkit-scrollbar-thumb { background: var(--border); border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: #555577; }
</style>
</head><body>" + body + "</body></html>";
    }
}
