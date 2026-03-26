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
            .Build();
    }

    public string RenderToHtml(string markdown, string? filePath = null, string theme = "dark")
    {
        var body = Markdown.ToHtml(markdown, _pipeline);
        return WrapInPage(body, filePath, theme);
    }

    private string WrapInPage(string body, string? filePath, string theme)
    {
        var baseTag = filePath != null
            ? "<base href=\"file:///" + filePath.Replace("\\", "/").Replace(" ", "%20") + "\">"
            : "";

        var css = GetThemeCss(theme);
        var hlCss = (theme == "light" || theme == "sepia")
            ? "atom-one-light" : "atom-one-dark";

        return "<!DOCTYPE html><html><head><meta charset='utf-8'>" + baseTag
            + "<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.css'>"
            + $"<link rel='stylesheet' href='https://cdn.jsdelivr.net/npm/highlight.js@11/dist/styles/{hlCss}.min.css'>"
            + "<style>" + GetCommonCss() + css + "</style>"
            + "</head><body>" + body
            + "<script src='https://cdn.jsdelivr.net/npm/highlight.js@11/dist/highlight.min.js'></script>"
            + "<script>hljs.highlightAll();</script>"
            + "<script defer src='https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/katex.min.js'></script>"
            + "<script defer src='https://cdn.jsdelivr.net/npm/katex@0.16.11/dist/contrib/auto-render.min.js'"
            + " onload=\"renderMathInElement(document.body,{delimiters:["
            + "{left:'$$',right:'$$',display:true},{left:'$',right:'$',display:false},"
            + "{left:'\\\\(',right:'\\\\)',display:false},{left:'\\\\[',right:'\\\\]',display:true}]})\"></script>"
            + "</body></html>";
    }

    private static string GetCommonCss() => @"
* { box-sizing: border-box; margin: 0; padding: 0; }
body {
  font-family: 'Segoe UI', 'Malgun Gothic', sans-serif;
  font-size: 15px;
  line-height: 1.75;
  max-width: 860px;
  margin: 0 auto;
  padding: 40px 32px 80px;
}
h1, h2, h3, h4, h5, h6 { font-weight: 600; margin-top: 1.6em; margin-bottom: 0.6em; line-height: 1.3; }
h1 { font-size: 2em; border-bottom: 2px solid var(--border); padding-bottom: 0.3em; color: var(--h1); }
h2 { font-size: 1.5em; border-bottom: 1px solid var(--border); padding-bottom: 0.2em; color: var(--h2); }
h3 { font-size: 1.25em; color: var(--h3); }
h4 { font-size: 1.1em; color: var(--h4); }
p { margin: 0.8em 0; }
a { color: var(--link); text-decoration: none; }
a:hover { text-decoration: underline; }
code {
  background: var(--code-bg);
  color: var(--code-text);
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
pre code { background: none; padding: 0; font-size: 0.9em; }
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
table { width: 100%; border-collapse: collapse; margin: 1em 0; font-size: 0.93em; }
th { background: var(--surface); color: var(--h2); padding: 0.6em 1em; border: 1px solid var(--border); text-align: left; font-weight: 600; }
td { padding: 0.5em 1em; border: 1px solid var(--border); }
tr:nth-child(even) td { background: var(--row-alt); }
img { max-width: 100%; border-radius: 6px; margin: 0.5em 0; }
input[type=checkbox] { accent-color: var(--h1); margin-right: 0.4em; }
.task-list-item { list-style: none; margin-left: -1.2em; }
mark { background: var(--mark-bg); color: var(--mark-text); padding: 0 0.2em; border-radius: 2px; }
del { color: var(--text-dim); }
sup, sub { font-size: 0.75em; }
::-webkit-scrollbar { width: 8px; height: 8px; }
::-webkit-scrollbar-track { background: var(--bg); }
::-webkit-scrollbar-thumb { background: var(--border); border-radius: 4px; }
::-webkit-scrollbar-thumb:hover { background: var(--text-dim); }
";

    private static string GetThemeCss(string theme) => theme switch
    {
        "github-dark" => @":root {
  --bg: #0d1117; --surface: #161b22; --border: #30363d;
  --text: #e6edf3; --text-dim: #8b949e;
  --h1: #58a6ff; --h2: #58a6ff; --h3: #79c0ff; --h4: #56d364;
  --code-bg: #161b22; --code-text: #f0883e;
  --blockquote-border: #3b82f6; --link: #58a6ff;
  --row-alt: rgba(255,255,255,0.03);
  --mark-bg: rgba(187,128,9,0.15); --mark-text: #e3b341;
}
html { background: var(--bg); color: var(--text); }",

        "nord" => @":root {
  --bg: #2e3440; --surface: #3b4252; --border: #434c5e;
  --text: #eceff4; --text-dim: #9099a7;
  --h1: #88c0d0; --h2: #81a1c1; --h3: #5e81ac; --h4: #a3be8c;
  --code-bg: #3b4252; --code-text: #d08770;
  --blockquote-border: #88c0d0; --link: #81a1c1;
  --row-alt: rgba(255,255,255,0.04);
  --mark-bg: rgba(235,203,139,0.15); --mark-text: #ebcb8b;
}
html { background: var(--bg); color: var(--text); }",

        "dracula" => @":root {
  --bg: #282a36; --surface: #313442; --border: #44475a;
  --text: #f8f8f2; --text-dim: #6272a4;
  --h1: #ff79c6; --h2: #bd93f9; --h3: #8be9fd; --h4: #50fa7b;
  --code-bg: #1e1f29; --code-text: #ffb86c;
  --blockquote-border: #ff79c6; --link: #8be9fd;
  --row-alt: rgba(255,255,255,0.04);
  --mark-bg: rgba(241,250,140,0.15); --mark-text: #f1fa8c;
}
html { background: var(--bg); color: var(--text); }",

        "sepia" => @":root {
  --bg: #f4ecd8; --surface: #ede0c4; --border: #c8b89a;
  --text: #433422; --text-dim: #806040;
  --h1: #7c4b00; --h2: #6b3c1a; --h3: #8b5e3c; --h4: #4a7c59;
  --code-bg: #ede0c4; --code-text: #9b3b00;
  --blockquote-border: #a87040; --link: #7c4b00;
  --row-alt: rgba(0,0,0,0.04);
  --mark-bg: rgba(255,220,80,0.35); --mark-text: #6b3c1a;
}
html { background: var(--bg); color: var(--text); }",

        "light" => @":root {
  --bg: #ffffff; --surface: #f6f8fa; --border: #d0d7de;
  --text: #1f2328; --text-dim: #57606a;
  --h1: #0969da; --h2: #0969da; --h3: #1a7f37; --h4: #8250df;
  --code-bg: #f6f8fa; --code-text: #e85d04;
  --blockquote-border: #0969da; --link: #0969da;
  --row-alt: rgba(0,0,0,0.03);
  --mark-bg: rgba(255,200,0,0.3); --mark-text: #6e4900;
}
html { background: var(--bg); color: var(--text); }",

        _ => @":root {
  --bg: #181e2e; --surface: #1e2840; --border: #2a3654;
  --text: #cdd6f4; --text-dim: #7a90b8;
  --h1: #89b4fa; --h2: #cba6f7; --h3: #94e2d5; --h4: #a6e3a1;
  --code-bg: #181825; --code-text: #fab387;
  --blockquote-border: #89b4fa; --link: #89dceb;
  --row-alt: rgba(255,255,255,0.03);
  --mark-bg: rgba(249,226,175,0.2); --mark-text: #f9e2af;
}
html { background: var(--bg); color: var(--text); }",
    };
}
