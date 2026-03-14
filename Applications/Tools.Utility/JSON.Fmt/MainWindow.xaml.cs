using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using JsonFmt.Services;

namespace JsonFmt;

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int size);

    private string _lastFormatted = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
    }

    private void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new WindowInteropHelper(this);
        int val = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref val, sizeof(int));
        StatusBar.Text = "JSON을 붙여넣거나 입력하세요.";
    }

    // ─── 툴바 버튼 ─────────────────────────────────────────────

    private void BtnPaste_Click(object sender, RoutedEventArgs e)
    {
        var text = Clipboard.GetText();
        if (string.IsNullOrEmpty(text)) { SetStatus("클립보드가 비어 있습니다.", error: true); return; }
        InputBox.Text = text;
        FormatInput(text);
    }

    private void BtnFormat_Click(object sender, RoutedEventArgs e)
    {
        FormatInput(InputBox.Text);
    }

    private void BtnMinify_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text)) return;
        var (doc, err) = TryParse(InputBox.Text);
        if (err != null) { ShowError(err); return; }
        var minified = JsonSerializer.Serialize(doc);
        SetOutputRaw(minified);
        SetStatus($"축소 완료 — {minified.Length} 바이트");
    }

    private void BtnCopy_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_lastFormatted))
        {
            Clipboard.SetText(_lastFormatted);
            SetStatus("클립보드에 복사되었습니다.");
        }
    }

    private void BtnClear_Click(object sender, RoutedEventArgs e)
    {
        InputBox.Text = string.Empty;
        ClearOutput();
        HideError();
        SetStatus("지워졌습니다.");
        BtnCopy.IsEnabled = false;
    }

    private void BtnSortKeys_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputBox.Text)) return;
        var (doc, err) = TryParse(InputBox.Text);
        if (err != null) { ShowError(err); return; }
        var sorted = SortKeys(doc!);
        var pretty = JsonSerializer.Serialize(sorted, new JsonSerializerOptions { WriteIndented = true });
        SetOutputHighlighted(pretty);
        _lastFormatted = pretty;
        BtnCopy.IsEnabled = true;
        SetStatus("키 정렬 완료");
    }

    private void BtnFix_Click(object sender, RoutedEventArgs e)
    {
        var normalized = JsonNormalizer.Normalize(InputBox.Text);
        if (normalized == InputBox.Text) { SetStatus("수정할 내용이 없습니다."); return; }
        InputBox.Text = normalized;
        SetStatus("Lenient 수정 적용됨 (주석·trailing comma·따옴표)");
        FormatInput(normalized);
    }

    // ─── 입력 이벤트 ─────────────────────────────────────────────

    private void InputBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        ValidateInput(InputBox.Text);
    }

    private void InputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.Control)
        {
            e.Handled = true;
            FormatInput(InputBox.Text);
        }
        if (e.Key == Key.V && Keyboard.Modifiers == ModifierKeys.Control)
        {
            Dispatcher.InvokeAsync(() => FormatInput(InputBox.Text),
                System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    // ─── 드래그앤드롭 ─────────────────────────────────────────────

    private void Window_DragEnter(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void Window_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Length == 0) return;
        try
        {
            var text = File.ReadAllText(files[0], Encoding.UTF8);
            InputBox.Text = text;
            FormatInput(text);
        }
        catch (Exception ex)
        {
            SetStatus($"파일 읽기 오류: {ex.Message}", error: true);
        }
    }

    // ─── 핵심 로직 ─────────────────────────────────────────────

    private void FormatInput(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { ClearOutput(); HideError(); return; }

        var (doc, err) = TryParse(raw);

        if (err != null)
        {
            var normalized = JsonNormalizer.Normalize(raw);
            var (doc2, err2) = TryParse(normalized);
            if (doc2 != null)
            {
                doc = doc2;
                err = null;
                SetStatus("⚠️ Lenient 파싱으로 처리됨 (원본에 비표준 구문 포함)");
            }
            else
            {
                ShowError(err);
                ClearOutput();
                return;
            }
        }

        HideError();
        var pretty = JsonSerializer.Serialize(doc, new JsonSerializerOptions { WriteIndented = true });
        _lastFormatted = pretty;
        SetOutputHighlighted(pretty);
        BtnCopy.IsEnabled = true;

        var lines = pretty.Count(c => c == '\n') + 1;
        StatsBar.Text = $"{lines}줄 · {pretty.Length}자";
        if (err == null) SetStatus("✅ 유효한 JSON");
    }

    private void ValidateInput(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) { HideError(); StatsBar.Text = ""; return; }
        var (_, err) = TryParse(raw);
        if (err == null) { HideError(); return; }
        var normalized = JsonNormalizer.Normalize(raw);
        var (_, err2) = TryParse(normalized);
        if (err2 == null)
        {
            ErrorPanel.Visibility = Visibility.Visible;
            ErrorTitle.Text = "⚠️ 비표준 구문 — Lenient 파싱으로 처리 가능 (Ctrl+Enter 또는 포맷 버튼)";
            ErrorDetail.Text = err.Message;
        }
        else
        {
            ShowError(err);
        }
    }

    private static (JsonNode? doc, JsonException? err) TryParse(string text)
    {
        try
        {
            var docOpts = new JsonDocumentOptions
            {
                AllowTrailingCommas = true,
                CommentHandling = JsonCommentHandling.Skip
            };
            using var doc = JsonDocument.Parse(text, docOpts);
            var node = JsonNode.Parse(doc.RootElement.GetRawText());
            return (node, null);
        }
        catch (JsonException ex)
        {
            return (null, ex);
        }
    }

    private static JsonNode? SortKeys(JsonNode? node)
    {
        if (node is JsonObject obj)
        {
            var keys = obj.Select(kv => kv.Key).OrderBy(k => k).ToList();
            var sorted = new JsonObject();
            foreach (var key in keys)
            {
                var val = obj[key]?.DeepClone();
                sorted[key] = SortKeys(val);
            }
            return sorted;
        }
        if (node is JsonArray arr)
        {
            var newArr = new JsonArray();
            foreach (var item in arr)
                newArr.Add(SortKeys(item?.DeepClone()));
            return newArr;
        }
        return node?.DeepClone();
    }

    // ─── 구문 강조 ─────────────────────────────────────────────

    private static readonly SolidColorBrush BrushKey      = new(Color.FromRgb(0x7E, 0xC8, 0xE3));
    private static readonly SolidColorBrush BrushString   = new(Color.FromRgb(0xCE, 0x9F, 0x89));
    private static readonly SolidColorBrush BrushNumber   = new(Color.FromRgb(0x8C, 0xC8, 0x94));
    private static readonly SolidColorBrush BrushBool     = new(Color.FromRgb(0xBB, 0x99, 0xFF));
    private static readonly SolidColorBrush BrushNull     = new(Color.FromRgb(0x88, 0x88, 0xAA));
    private static readonly SolidColorBrush BrushPunct    = new(Color.FromRgb(0x66, 0x66, 0x88));
    private static readonly SolidColorBrush BrushDefault  = new(Color.FromRgb(0xE0, 0xE0, 0xE0));

    private enum TokKind { Key, String, Number, Bool, Null, Punct, Whitespace, Other }

    private void SetOutputHighlighted(string text)
    {
        OutputDoc.Blocks.Clear();
        var paragraph = new Paragraph { Margin = new Thickness(0) };
        var tokens = Tokenize(text);
        foreach (var (tok, kind) in tokens)
        {
            var run = new Run(tok)
            {
                Foreground = kind switch
                {
                    TokKind.Key       => BrushKey,
                    TokKind.String    => BrushString,
                    TokKind.Number    => BrushNumber,
                    TokKind.Bool      => BrushBool,
                    TokKind.Null      => BrushNull,
                    TokKind.Punct     => BrushPunct,
                    _                 => BrushDefault
                }
            };
            paragraph.Inlines.Add(run);
        }
        OutputDoc.Blocks.Add(paragraph);
    }

    private void SetOutputRaw(string text)
    {
        OutputDoc.Blocks.Clear();
        _lastFormatted = text;
        BtnCopy.IsEnabled = true;
        OutputDoc.Blocks.Add(new Paragraph(new Run(text))
        {
            Margin = new Thickness(0),
            Foreground = BrushDefault
        });
    }

    private static List<(string tok, TokKind kind)> Tokenize(string json)
    {
        var result = new List<(string, TokKind)>();
        int i = 0;
        bool expectKey = false;
        var stack = new Stack<char>();

        while (i < json.Length)
        {
            char c = json[i];

            if (char.IsWhiteSpace(c))
            {
                int start = i;
                while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
                result.Add((json[start..i], TokKind.Whitespace));
                continue;
            }

            if (c == '{')
            {
                stack.Push('{'); expectKey = true;
                result.Add(("{", TokKind.Punct)); i++; continue;
            }
            if (c == '[')
            {
                stack.Push('['); expectKey = false;
                result.Add(("[", TokKind.Punct)); i++; continue;
            }
            if (c == '}')
            {
                if (stack.Count > 0) stack.Pop();
                expectKey = stack.Count > 0 && stack.Peek() == '{';
                result.Add(("}", TokKind.Punct)); i++; continue;
            }
            if (c == ']')
            {
                if (stack.Count > 0) stack.Pop();
                expectKey = stack.Count > 0 && stack.Peek() == '{';
                result.Add(("]", TokKind.Punct)); i++; continue;
            }
            if (c == ':') { expectKey = false; result.Add((":", TokKind.Punct)); i++; continue; }
            if (c == ',')
            {
                expectKey = stack.Count > 0 && stack.Peek() == '{';
                result.Add((",", TokKind.Punct)); i++; continue;
            }

            // 문자열
            if (c == '"')
            {
                var sb = new StringBuilder();
                sb.Append('"');
                i++;
                while (i < json.Length)
                {
                    char ch = json[i++];
                    sb.Append(ch);
                    if (ch == '\\' && i < json.Length) { sb.Append(json[i++]); }
                    else if (ch == '"') break;
                }
                result.Add((sb.ToString(), expectKey ? TokKind.Key : TokKind.String));
                continue;
            }

            // 숫자
            if (c == '-' || char.IsDigit(c))
            {
                int start = i;
                if (json[i] == '-') i++;
                while (i < json.Length && (char.IsDigit(json[i]) || json[i] == '.' ||
                       json[i] == 'e' || json[i] == 'E' || json[i] == '+' ||
                       (json[i] == '-' && i > start && (json[i-1] == 'e' || json[i-1] == 'E')))) i++;
                result.Add((json[start..i], TokKind.Number));
                continue;
            }

            if (i + 4 <= json.Length && json[i..(i+4)] == "true")  { result.Add(("true",  TokKind.Bool)); i += 4; continue; }
            if (i + 5 <= json.Length && json[i..(i+5)] == "false") { result.Add(("false", TokKind.Bool)); i += 5; continue; }
            if (i + 4 <= json.Length && json[i..(i+4)] == "null")  { result.Add(("null",  TokKind.Null)); i += 4; continue; }

            result.Add((c.ToString(), TokKind.Other));
            i++;
        }
        return result;
    }

    private void ClearOutput()
    {
        OutputDoc.Blocks.Clear();
        _lastFormatted = string.Empty;
        StatsBar.Text = "";
        BtnCopy.IsEnabled = false;
    }

    private void ShowError(JsonException ex)
    {
        ErrorPanel.Visibility = Visibility.Visible;
        var loc = ex.LineNumber.HasValue ? $" — 줄 {ex.LineNumber + 1}, 열 {ex.BytePositionInLine + 1}" : "";
        ErrorTitle.Text = $"❌ JSON 파싱 오류{loc}";
        ErrorDetail.Text = ex.Message;
    }

    private void HideError()
    {
        ErrorPanel.Visibility = Visibility.Collapsed;
    }

    private void SetStatus(string msg, bool error = false)
    {
        StatusBar.Text = msg;
        StatusBar.Foreground = error
            ? new SolidColorBrush(Color.FromRgb(0xFF, 0x88, 0x88))
            : new SolidColorBrush(Color.FromRgb(0x66, 0x66, 0x88));
    }
}
