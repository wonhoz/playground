namespace AIPForge.Models;

/// <summary>AIP XML 요소의 속성 하나 — 원본 순서를 유지하기 위해 리스트로 보관</summary>
public sealed class AipAttr(string name, string value)
{
    public string Name { get; set; } = name;
    public string Value { get; set; } = value;
}

/// <summary>
/// AIP 문서의 요소 노드 (DOCUMENT · COMPONENT · ROW · ATTRIBUTE).
/// XDocument 대신 직접 보관하는 이유: Advanced Installer 고유의 직렬화 형식
/// (속성 순서 · <c>&amp;gt;</c> 이스케이프 · <c>"/&gt;</c> 공백 없음)을 바이트 단위로 재현하기 위함.
/// </summary>
public sealed class AipElement(string name)
{
    public string Name { get; set; } = name;
    public List<AipAttr> Attributes { get; } = [];
    public List<AipElement> Children { get; } = [];
    public AipElement? Parent { get; set; }

    /// <summary>텍스트 노드 (AIP 표준 파일에는 없지만 손상 방지용으로 보존)</summary>
    public string? Text { get; set; }

    public string? Get(string name)
    {
        foreach (var a in Attributes)
            if (a.Name == name) return a.Value;
        return null;
    }

    public bool Has(string name) => Attributes.Exists(a => a.Name == name);

    /// <summary>
    /// 속성 설정. 새 속성은 <paramref name="columnOrder"/>(열 순서) 기준으로
    /// 뒤에 오는 기존 속성 앞에 끼워 넣어 AI의 스키마 순서를 최대한 유지한다.
    /// </summary>
    public void Set(string name, string value, IReadOnlyList<string>? columnOrder = null)
    {
        var existing = Attributes.Find(a => a.Name == name);
        if (existing != null)
        {
            existing.Value = value;
            return;
        }

        var insertAt = Attributes.Count;
        if (columnOrder != null)
        {
            var myIndex = IndexOf(columnOrder, name);
            if (myIndex >= 0)
            {
                for (var i = 0; i < Attributes.Count; i++)
                {
                    var idx = IndexOf(columnOrder, Attributes[i].Name);
                    if (idx > myIndex) { insertAt = i; break; }
                }
            }
        }
        Attributes.Insert(insertAt, new AipAttr(name, value));
    }

    public bool Remove(string name) => Attributes.RemoveAll(a => a.Name == name) > 0;

    public void AddChild(AipElement child, int index = -1)
    {
        child.Parent = this;
        if (index < 0 || index > Children.Count) Children.Add(child);
        else Children.Insert(index, child);
    }

    public AipElement Clone()
    {
        var copy = new AipElement(Name) { Text = Text };
        foreach (var a in Attributes) copy.Attributes.Add(new AipAttr(a.Name, a.Value));
        foreach (var c in Children) copy.AddChild(c.Clone());
        return copy;
    }

    private static int IndexOf(IReadOnlyList<string> list, string value)
    {
        for (var i = 0; i < list.Count; i++)
            if (list[i] == value) return i;
        return -1;
    }
}
