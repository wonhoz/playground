using System.Xml.Linq;

namespace SVG.Forge.Services;

public static class SvgSerializer
{
    static readonly CultureInfo C = CultureInfo.InvariantCulture;

    public static void Save(SvgDocument doc, string path)
    {
        var svg = new XElement("svg",
            new XAttribute("xmlns", "http://www.w3.org/2000/svg"),
            new XAttribute("width",   F(doc.CanvasWidth)),
            new XAttribute("height",  F(doc.CanvasHeight)),
            new XAttribute("viewBox", $"0 0 {F(doc.CanvasWidth)} {F(doc.CanvasHeight)}"),
            new XAttribute("data-bg", ToHex(doc.Background)));

        foreach (var layer in doc.Layers)
        {
            var g = new XElement("g",
                new XAttribute("id",         $"layer-{doc.Layers.IndexOf(layer)}"),
                new XAttribute("data-name",  layer.Name),
                new XAttribute("visibility", layer.IsVisible ? "visible" : "hidden"));
            foreach (var el in layer.Elements)
                g.Add(Serialize(el));
            svg.Add(g);
        }

        new XDocument(new XDeclaration("1.0", "utf-8", null), svg).Save(path);
    }

    static XElement Serialize(SvgElement el)
    {
        var fill   = el.HasFill   ? ToHex(el.FillColor)   : "none";
        var stroke = el.HasStroke ? ToHex(el.StrokeColor) : "none";

        XElement xe = el.ShapeType switch
        {
            SvgShapeType.Rect => new XElement("rect",
                new XAttribute("x",      F(el.X)), new XAttribute("y",      F(el.Y)),
                new XAttribute("width",  F(el.W)), new XAttribute("height", F(el.H))),

            SvgShapeType.Ellipse => new XElement("ellipse",
                new XAttribute("cx", F(el.X + el.W / 2)), new XAttribute("cy", F(el.Y + el.H / 2)),
                new XAttribute("rx", F(el.W / 2)),         new XAttribute("ry", F(el.H / 2))),

            SvgShapeType.Line => new XElement("line",
                new XAttribute("x1", F(el.X)),  new XAttribute("y1", F(el.Y)),
                new XAttribute("x2", F(el.X2)), new XAttribute("y2", F(el.Y2))),

            SvgShapeType.Text => new XElement("text",
                new XAttribute("x",           F(el.X)),
                new XAttribute("y",           F(el.Y + el.FontSize)),
                new XAttribute("font-family", el.FontFamily),
                new XAttribute("font-size",   F(el.FontSize)),
                el.Text),

            _ => new XElement("rect")
        };

        xe.Add(new XAttribute("fill",         fill));
        xe.Add(new XAttribute("stroke",       stroke));
        xe.Add(new XAttribute("stroke-width", F(el.StrokeWidth)));
        if (el.Opacity < 1.0) xe.Add(new XAttribute("opacity", F(el.Opacity)));
        xe.Add(new XAttribute("data-id",   el.Id));
        xe.Add(new XAttribute("data-name", el.Name));
        return xe;
    }

    public static SvgDocument Load(string path)
    {
        var xd  = XDocument.Load(path);
        var svg = xd.Root ?? throw new Exception("SVG 루트 요소를 찾을 수 없습니다.");
        var ns  = svg.Name.Namespace;

        var doc = new SvgDocument
        {
            FilePath     = path,
            CanvasWidth  = ParseD(svg.Attribute("width")?.Value)  ?? 800,
            CanvasHeight = ParseD(svg.Attribute("height")?.Value) ?? 600,
        };

        var bgAttr = svg.Attribute("data-bg")?.Value;
        if (bgAttr != null) doc.Background = FromHex(bgAttr);

        foreach (var g in svg.Elements(ns + "g"))
        {
            var layer = new SvgLayer
            {
                Name      = g.Attribute("data-name")?.Value ?? "레이어",
                IsVisible = g.Attribute("visibility")?.Value != "hidden"
            };
            foreach (var child in g.Elements())
                if (Deserialize(child, ns) is SvgElement el) layer.Elements.Add(el);
            doc.Layers.Add(layer);
        }

        if (doc.Layers.Count == 0)
        {
            var layer = new SvgLayer { Name = "레이어 1" };
            foreach (var child in svg.Elements())
                if (Deserialize(child, ns) is SvgElement el) layer.Elements.Add(el);
            doc.Layers.Add(layer);
        }

        return doc;
    }

    static SvgElement? Deserialize(XElement xe, XNamespace ns)
    {
        var local = xe.Name.LocalName;
        SvgElement? el = local switch
        {
            "rect" => new SvgElement
            {
                ShapeType = SvgShapeType.Rect,
                X = ParseD(xe.Attribute("x")?.Value)      ?? 0,
                Y = ParseD(xe.Attribute("y")?.Value)      ?? 0,
                W = ParseD(xe.Attribute("width")?.Value)  ?? 100,
                H = ParseD(xe.Attribute("height")?.Value) ?? 60,
            },
            "ellipse" => new SvgElement
            {
                ShapeType = SvgShapeType.Ellipse,
                X = (ParseD(xe.Attribute("cx")?.Value) ?? 0) - (ParseD(xe.Attribute("rx")?.Value) ?? 50),
                Y = (ParseD(xe.Attribute("cy")?.Value) ?? 0) - (ParseD(xe.Attribute("ry")?.Value) ?? 30),
                W = (ParseD(xe.Attribute("rx")?.Value) ?? 50) * 2,
                H = (ParseD(xe.Attribute("ry")?.Value) ?? 30) * 2,
            },
            "line" => new SvgElement
            {
                ShapeType = SvgShapeType.Line,
                X  = ParseD(xe.Attribute("x1")?.Value) ?? 0,
                Y  = ParseD(xe.Attribute("y1")?.Value) ?? 0,
                X2 = ParseD(xe.Attribute("x2")?.Value) ?? 100,
                Y2 = ParseD(xe.Attribute("y2")?.Value) ?? 100,
            },
            "text" => new SvgElement
            {
                ShapeType  = SvgShapeType.Text,
                X          = ParseD(xe.Attribute("x")?.Value)         ?? 0,
                Y          = ParseD(xe.Attribute("y")?.Value)         ?? 20,
                FontFamily = xe.Attribute("font-family")?.Value       ?? "Segoe UI",
                FontSize   = ParseD(xe.Attribute("font-size")?.Value) ?? 16,
                Text       = xe.Value,
            },
            _ => null
        };

        if (el == null) return null;

        var fillAttr = xe.Attribute("fill")?.Value;
        if (fillAttr == "none") el.HasFill = false;
        else if (fillAttr?.StartsWith("#") == true) { el.HasFill = true; el.FillColor = FromHex(fillAttr); }

        var strokeAttr = xe.Attribute("stroke")?.Value;
        if (strokeAttr == "none") el.HasStroke = false;
        else if (strokeAttr?.StartsWith("#") == true) { el.HasStroke = true; el.StrokeColor = FromHex(strokeAttr); }

        if (ParseD(xe.Attribute("stroke-width")?.Value) is double sw) el.StrokeWidth = sw;
        if (ParseD(xe.Attribute("opacity")?.Value) is double op) el.Opacity = op;
        el.Name = xe.Attribute("data-name")?.Value ?? local;
        return el;
    }

    static string F(double v) => v.ToString("F2", C);
    static string ToHex(Color c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}";

    static Color FromHex(string hex)
    {
        hex = hex.TrimStart('#');
        if (hex.Length == 6)
            return Color.FromRgb(
                Convert.ToByte(hex[..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16));
        return Colors.Black;
    }

    static double? ParseD(string? s) =>
        s != null && double.TryParse(s, NumberStyles.Float, C, out var v) ? v : null;
}
