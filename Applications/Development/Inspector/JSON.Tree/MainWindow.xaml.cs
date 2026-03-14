using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Tomlyn;
using Tomlyn.Model;
using YamlDotNet.Serialization;

namespace JsonTree;

enum NodeKind { Object, Array, String, Number, Boolean, Null }

record JsonNode(string Key, NodeKind Kind, string? Value, List<JsonNode>? Children, string Path, bool IsAdded = false, bool IsRemoved = false, bool IsChanged = false);

public partial class MainWindow : Window
{
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int val, int sz);

    string? _rawA, _rawB;
    JsonNode? _rootA, _rootB;

    public MainWindow() => InitializeComponent();

    void Window_Loaded(object sender, RoutedEventArgs e)
    {
        var helper = new System.Windows.Interop.WindowInteropHelper(this);
        int dark = 1;
        DwmSetWindowAttribute(helper.Handle, 20, ref dark, sizeof(int));
    }

    // ─── 파일 열기 ──────────────────────────────────────────────────────────
    void BtnOpenA_Click(object sender, RoutedEventArgs e) => OpenFile(isA: true);
    void BtnOpenB_Click(object sender, RoutedEventArgs e) => OpenFile(isA: false);

    void OpenFile(bool isA)
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "구조화 데이터|*.json;*.yaml;*.yml;*.toml|모든 파일|*.*",
            Title = isA ? "파일 열기" : "비교 파일 열기"
        };
        if (dlg.ShowDialog() != true) return;
        LoadFile(File.ReadAllText(dlg.FileName, Encoding.UTF8), Path.GetFileName(dlg.FileName), isA);
    }

    void BtnClipboard_Click(object sender, RoutedEventArgs e)
    {
        if (!Clipboard.ContainsText()) { StatusBar.Text = "클립보드에 텍스트가 없습니다."; return; }
        LoadFile(Clipboard.GetText(), "클립보드", isA: true);
    }

    void Window_DragEnter(object sender, DragEventArgs e)
        => e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    void Window_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
        {
            bool isA = _rawA is null || files.Length == 1;
            foreach (var f in files.Take(2))
            {
                LoadFile(File.ReadAllText(f, Encoding.UTF8), Path.GetFileName(f), isA);
                isA = false;
            }
        }
    }

    void LoadFile(string text, string label, bool isA)
    {
        try
        {
            var root = ParseText(text);
            if (isA)
            {
                _rawA = text; _rootA = root;
                LabelA.Text = label;
                StatsA.Text = $"  ({CountNodes(root)} 노드)";
                BtnExport.IsEnabled = true;
            }
            else
            {
                _rawB = text; _rootB = root;
                LabelB.Text = label;
                StatsB.Text = $"  ({CountNodes(root)} 노드)";
                BtnClearB.IsEnabled = true;
                EnableDiffLayout(true);
            }

            if (_rootB is not null)
                BuildDiffTrees();
            else
            {
                BuildTree(TreeA, _rootA!, string.Empty);
            }

            StatusBar.Text = $"로드됨: {label}";
        }
        catch (Exception ex)
        {
            MessageBox.Show($"파싱 오류: {ex.Message}", "JSON.Tree", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    void BtnClearB_Click(object sender, RoutedEventArgs e)
    {
        _rawB = null; _rootB = null;
        TreeB.Items.Clear();
        EnableDiffLayout(false);
        BtnClearB.IsEnabled = false;
        if (_rootA is not null) BuildTree(TreeA, _rootA, string.Empty);
        StatusBar.Text = "비교 모드 해제";
    }

    void EnableDiffLayout(bool enable)
    {
        ColSep.Width = enable ? new GridLength(6) : new GridLength(0);
        ColB.Width = enable ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
    }

    // ─── 파싱 ────────────────────────────────────────────────────────────────
    static JsonNode ParseText(string text)
    {
        text = text.Trim();

        // JSON 시도
        if (text.StartsWith('{') || text.StartsWith('['))
        {
            var doc = JsonDocument.Parse(text, new JsonDocumentOptions { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip });
            return ParseElement(doc.RootElement, "$", "$");
        }

        // YAML 시도
        try
        {
            var deserializer = new DeserializerBuilder().Build();
            var obj = deserializer.Deserialize<object>(text);
            return ParseObject(obj, "$", "$");
        }
        catch { }

        // TOML 시도
        try
        {
            var model = Toml.ToModel(text);
            return ParseTomlTable(model, "$", "$");
        }
        catch { }

        throw new FormatException("JSON / YAML / TOML 형식을 인식할 수 없습니다.");
    }

    static JsonNode ParseElement(JsonElement el, string key, string path) => el.ValueKind switch
    {
        JsonValueKind.Object => new JsonNode(key, NodeKind.Object, null,
            el.EnumerateObject().Select(p => ParseElement(p.Value, p.Name, $"{path}.{p.Name}")).ToList(), path),
        JsonValueKind.Array => new JsonNode(key, NodeKind.Array, null,
            el.EnumerateArray().Select((v, i) => ParseElement(v, $"[{i}]", $"{path}[{i}]")).ToList(), path),
        JsonValueKind.String => new JsonNode(key, NodeKind.String, el.GetString() ?? "", null, path),
        JsonValueKind.Number => new JsonNode(key, NodeKind.Number, el.GetRawText(), null, path),
        JsonValueKind.True or JsonValueKind.False => new JsonNode(key, NodeKind.Boolean, el.GetBoolean().ToString().ToLower(), null, path),
        _ => new JsonNode(key, NodeKind.Null, "null", null, path)
    };

    static JsonNode ParseObject(object? obj, string key, string path)
    {
        return obj switch
        {
            Dictionary<object, object> dict => new JsonNode(key, NodeKind.Object, null,
                dict.Select(kv => ParseObject(kv.Value, kv.Key.ToString()!, $"{path}.{kv.Key}")).ToList(), path),
            List<object> list => new JsonNode(key, NodeKind.Array, null,
                list.Select((v, i) => ParseObject(v, $"[{i}]", $"{path}[{i}]")).ToList(), path),
            string s => new JsonNode(key, NodeKind.String, s, null, path),
            bool b => new JsonNode(key, NodeKind.Boolean, b.ToString().ToLower(), null, path),
            null => new JsonNode(key, NodeKind.Null, "null", null, path),
            _ => new JsonNode(key, NodeKind.Number, obj.ToString() ?? "", null, path)
        };
    }

    static JsonNode ParseTomlTable(TomlTable table, string key, string path)
    {
        var children = new List<JsonNode>();
        foreach (var kv in table)
        {
            string childPath = $"{path}.{kv.Key}";
            children.Add(kv.Value switch
            {
                TomlTable sub => ParseTomlTable(sub, kv.Key, childPath),
                TomlArray arr => ParseTomlArray(arr, kv.Key, childPath),
                _ => new JsonNode(kv.Key, InferKind(kv.Value), kv.Value?.ToString() ?? "null", null, childPath)
            });
        }
        return new JsonNode(key, NodeKind.Object, null, children, path);
    }

    static JsonNode ParseTomlArray(TomlArray arr, string key, string path)
    {
        var children = arr.Select((v, i) => v is TomlTable sub
            ? ParseTomlTable(sub, $"[{i}]", $"{path}[{i}]")
            : new JsonNode($"[{i}]", InferKind(v), v?.ToString() ?? "null", null, $"{path}[{i}]")).ToList();
        return new JsonNode(key, NodeKind.Array, null, children, path);
    }

    static NodeKind InferKind(object? v) => v switch
    {
        string => NodeKind.String,
        bool => NodeKind.Boolean,
        null => NodeKind.Null,
        _ => NodeKind.Number
    };

    // ─── 트리 빌드 ───────────────────────────────────────────────────────────
    void BuildTree(TreeView treeView, JsonNode root, string filter)
    {
        treeView.Items.Clear();
        var item = CreateTreeItem(root, filter);
        if (item is null) return;
        treeView.Items.Add(item);
        item.IsExpanded = true;
    }

    TreeViewItem? CreateTreeItem(JsonNode node, string filter)
    {
        bool matchSelf = string.IsNullOrWhiteSpace(filter)
            || node.Key.Contains(filter, StringComparison.OrdinalIgnoreCase)
            || (node.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

        var childItems = node.Children?.Select(c => CreateTreeItem(c, filter)).Where(i => i is not null).ToList();
        bool hasMatchingChildren = childItems?.Count > 0;

        if (!matchSelf && !hasMatchingChildren) return null;

        var item = new TreeViewItem
        {
            Header = BuildHeader(node, filter),
            IsExpanded = !string.IsNullOrWhiteSpace(filter),
            Tag = node.Path
        };
        if (childItems is not null)
            foreach (var c in childItems)
                item.Items.Add(c!);

        return item;
    }

    static FrameworkElement BuildHeader(JsonNode node, string filter)
    {
        var sp = new StackPanel { Orientation = Orientation.Horizontal };

        // 노드 유형 아이콘
        string icon = node.Kind switch
        {
            NodeKind.Object  => "{}",
            NodeKind.Array   => "[]",
            NodeKind.String  => "\"\"",
            NodeKind.Number  => "##",
            NodeKind.Boolean => "✓✗",
            _                => "∅"
        };
        var iconColor = node.Kind switch
        {
            NodeKind.Object  => Color.FromRgb(0x5A, 0x7F, 0xD6),
            NodeKind.Array   => Color.FromRgb(0xD6, 0x9A, 0x3A),
            NodeKind.String  => Color.FromRgb(0x78, 0xCC, 0x78),
            NodeKind.Number  => Color.FromRgb(0xDD, 0x77, 0x77),
            NodeKind.Boolean => Color.FromRgb(0xCC, 0x88, 0xDD),
            _                => Color.FromRgb(0x77, 0x77, 0x99)
        };

        sp.Children.Add(new TextBlock
        {
            Text = icon,
            Width = 24, TextAlignment = TextAlignment.Center,
            FontFamily = new FontFamily("Consolas"), FontSize = 10,
            Foreground = new SolidColorBrush(iconColor),
            Margin = new Thickness(0, 0, 6, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        // Diff 색상
        Color? diffBg = node.IsAdded ? Color.FromArgb(40, 0, 200, 80)
                      : node.IsRemoved ? Color.FromArgb(40, 200, 50, 50)
                      : node.IsChanged ? Color.FromArgb(40, 200, 180, 50)
                      : (Color?)null;

        // Key
        bool keyMatch = !string.IsNullOrWhiteSpace(filter)
            && node.Key.Contains(filter, StringComparison.OrdinalIgnoreCase);

        var keyTb = new TextBlock
        {
            FontFamily = new FontFamily("Consolas"), FontSize = 12,
            Foreground = new SolidColorBrush(keyMatch ? Color.FromRgb(0xFF, 0xE0, 0x60)
                                                       : Color.FromRgb(0xCC, 0xCC, 0xEE))
        };
        keyTb.Text = node.Key;

        sp.Children.Add(keyTb);

        // 값 (리프 노드)
        if (node.Value is not null)
        {
            sp.Children.Add(new TextBlock
            {
                Text = ": ",
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77)),
                FontFamily = new FontFamily("Consolas"), FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });

            bool valMatch = !string.IsNullOrWhiteSpace(filter)
                && (node.Value?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);

            string displayVal = node.Kind == NodeKind.String
                ? $"\"{TruncateVal(node.Value!, 80)}\""
                : TruncateVal(node.Value!, 80);

            sp.Children.Add(new TextBlock
            {
                Text = displayVal,
                FontFamily = new FontFamily("Consolas"), FontSize = 12,
                Foreground = new SolidColorBrush(valMatch ? Color.FromRgb(0xFF, 0xE0, 0x60)
                                                           : iconColor),
                VerticalAlignment = VerticalAlignment.Center
            });
        }
        else if (node.Children is not null)
        {
            sp.Children.Add(new TextBlock
            {
                Text = $"  ({node.Children.Count})",
                Foreground = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x77)),
                FontFamily = new FontFamily("Consolas"), FontSize = 11,
                VerticalAlignment = VerticalAlignment.Center
            });
        }

        // Diff 배경
        if (diffBg.HasValue)
            return new Border { Background = new SolidColorBrush(diffBg.Value), CornerRadius = new CornerRadius(3), Child = sp };

        return sp;
    }

    static string TruncateVal(string s, int max) => s.Length > max ? s[..max] + "…" : s;

    // ─── Diff ────────────────────────────────────────────────────────────────
    void BuildDiffTrees()
    {
        if (_rootA is null || _rootB is null) return;
        var (diffA, diffB) = DiffNodes(_rootA, _rootB);
        BuildTree(TreeA, diffA, string.Empty);
        BuildTree(TreeB, diffB, string.Empty);
        if (TreeA.Items[0] is TreeViewItem ia) ia.IsExpanded = true;
        if (TreeB.Items[0] is TreeViewItem ib) ib.IsExpanded = true;

        int added = Count(diffB, n => n.IsAdded);
        int removed = Count(diffA, n => n.IsRemoved);
        int changed = Count(diffA, n => n.IsChanged);
        StatusBar.Text = $"Diff: +{added}개 추가  -{removed}개 삭제  ~{changed}개 변경";
    }

    static int Count(JsonNode node, Func<JsonNode, bool> pred)
    {
        int c = pred(node) ? 1 : 0;
        if (node.Children is not null)
            c += node.Children.Sum(ch => Count(ch, pred));
        return c;
    }

    static (JsonNode a, JsonNode b) DiffNodes(JsonNode a, JsonNode b)
    {
        if (a.Kind != b.Kind)
            return (a with { IsChanged = true }, b with { IsChanged = true });

        if (a.Children is null && b.Children is null)
        {
            bool same = a.Value == b.Value;
            return (a with { IsChanged = !same }, b with { IsChanged = !same });
        }

        if (a.Children is null || b.Children is null)
            return (a with { IsChanged = true }, b with { IsChanged = true });

        // 오브젝트: 키 매칭
        if (a.Kind == NodeKind.Object)
        {
            var aDict = a.Children.ToDictionary(n => n.Key);
            var bDict = b.Children.ToDictionary(n => n.Key);
            var aKids = new List<JsonNode>();
            var bKids = new List<JsonNode>();

            foreach (var key in aDict.Keys.Union(bDict.Keys))
            {
                bool inA = aDict.TryGetValue(key, out var na);
                bool inB = bDict.TryGetValue(key, out var nb);
                if (inA && inB)
                {
                    var (da, db) = DiffNodes(na!, nb!);
                    aKids.Add(da); bKids.Add(db);
                }
                else if (inA)
                {
                    aKids.Add(na! with { IsRemoved = true });
                    bKids.Add(nb ?? na! with { IsAdded = true, IsRemoved = false });
                }
                else
                {
                    aKids.Add(na ?? nb! with { IsAdded = true, IsRemoved = false });
                    bKids.Add(nb! with { IsAdded = true });
                }
            }
            return (a with { Children = aKids }, b with { Children = bKids });
        }

        // 배열: 인덱스 매칭
        {
            int maxLen = Math.Max(a.Children.Count, b.Children.Count);
            var aKids = new List<JsonNode>();
            var bKids = new List<JsonNode>();
            for (int i = 0; i < maxLen; i++)
            {
                bool inA = i < a.Children.Count;
                bool inB = i < b.Children.Count;
                if (inA && inB)
                {
                    var (da, db) = DiffNodes(a.Children[i], b.Children[i]);
                    aKids.Add(da); bKids.Add(db);
                }
                else if (inA)
                {
                    aKids.Add(a.Children[i] with { IsRemoved = true });
                    bKids.Add(b.Children[i] with { IsAdded = true });
                }
                else
                {
                    aKids.Add(a.Children[i] with { IsAdded = true });
                    bKids.Add(b.Children[i] with { IsAdded = true });
                }
            }
            return (a with { Children = aKids }, b with { Children = bKids });
        }
    }

    // ─── 검색 ────────────────────────────────────────────────────────────────
    void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        string q = SearchBox.Text.Trim();
        if (_rootA is null) return;

        if (_rootB is not null)
            BuildDiffTrees();
        else
            BuildTree(TreeA, _rootA, q);

        // 매칭 수 계산
        if (!string.IsNullOrWhiteSpace(q) && _rootA is not null)
        {
            int cnt = CountMatch(_rootA, q);
            SearchCount.Text = $"{cnt}개 매칭";
        }
        else
            SearchCount.Text = "";
    }

    static int CountMatch(JsonNode node, string q)
    {
        int c = (node.Key.Contains(q, StringComparison.OrdinalIgnoreCase)
              || (node.Value?.Contains(q, StringComparison.OrdinalIgnoreCase) ?? false)) ? 1 : 0;
        return c + (node.Children?.Sum(ch => CountMatch(ch, q)) ?? 0);
    }

    void BtnExpandAll_Click(object sender, RoutedEventArgs e)
    {
        ExpandAll(TreeA, true);
        ExpandAll(TreeB, true);
    }

    static void ExpandAll(ItemsControl ic, bool expand)
    {
        foreach (var obj in ic.Items)
        {
            if (ic.ItemContainerGenerator.ContainerFromItem(obj) is TreeViewItem item)
            {
                item.IsExpanded = expand;
                ExpandAll(item, expand);
            }
        }
    }

    // ─── 내보내기 ────────────────────────────────────────────────────────────
    void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        if (_rootA is null) return;
        string fmt = (ExportFmt.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "JSON";
        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter = fmt switch { "YAML" => "YAML|*.yaml", "TOML" => "TOML|*.toml", _ => "JSON|*.json" },
            DefaultExt = fmt.ToLower()
        };
        if (dlg.ShowDialog() != true) return;

        try
        {
            string output = fmt switch
            {
                "YAML" => ConvertToYaml(_rootA),
                "TOML" => ConvertToToml(_rootA),
                _ => ConvertToJson(_rootA)
            };
            File.WriteAllText(dlg.FileName, output, new UTF8Encoding(true));
            StatusBar.Text = $"저장됨: {dlg.FileName}";
        }
        catch (Exception ex) { MessageBox.Show(ex.Message, "저장 오류"); }
    }

    static string ConvertToJson(JsonNode root)
    {
        var obj = NodeToObject(root);
        return JsonSerializer.Serialize(obj, new JsonSerializerOptions { WriteIndented = true });
    }

    static string ConvertToYaml(JsonNode root)
    {
        var obj = NodeToObject(root);
        var serializer = new SerializerBuilder().Build();
        return serializer.Serialize(obj);
    }

    static string ConvertToToml(JsonNode root)
    {
        // TOML 변환은 간단한 키=값 형식으로 (중첩 테이블 지원)
        var sb = new StringBuilder();
        WriteToml(sb, root, "");
        return sb.ToString();
    }

    static void WriteToml(StringBuilder sb, JsonNode node, string prefix)
    {
        if (node.Children is null)
        {
            string val = node.Kind == NodeKind.String ? $"\"{node.Value}\"" : (node.Value ?? "null");
            sb.AppendLine($"{prefix} = {val}");
            return;
        }
        if (node.Kind == NodeKind.Object)
        {
            if (!string.IsNullOrEmpty(prefix)) sb.AppendLine($"[{prefix}]");
            foreach (var c in node.Children)
                WriteToml(sb, c, string.IsNullOrEmpty(prefix) ? c.Key : $"{prefix}.{c.Key}");
        }
    }

    static object? NodeToObject(JsonNode node) => node.Kind switch
    {
        NodeKind.Object => node.Children!.ToDictionary(c => c.Key, c => NodeToObject(c)),
        NodeKind.Array => node.Children!.Select(c => NodeToObject(c)).ToList(),
        NodeKind.String => node.Value,
        NodeKind.Number => double.TryParse(node.Value, out double d) ? (object)d : node.Value!,
        NodeKind.Boolean => node.Value == "true",
        _ => null
    };

    // ─── 선택 노드 경로 표시 ────────────────────────────────────────────────
    void TreeA_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (TreeA.SelectedItem is TreeViewItem item && item.Tag is string path)
            PathBar.Text = path;
    }

    // ─── 통계 ────────────────────────────────────────────────────────────────
    static int CountNodes(JsonNode node) =>
        1 + (node.Children?.Sum(CountNodes) ?? 0);
}
