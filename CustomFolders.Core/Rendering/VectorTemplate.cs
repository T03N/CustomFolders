// SPDX-License-Identifier: GPL-3.0-or-later
using System.Globalization;
using System.Xml;
using System.Xml.Linq;
using SkiaSharp;

namespace CustomFolders.Rendering;

/// <summary>Compiles the built-in SVG shapes, gradients, and clips. Not an imported-file SVG engine.</summary>
internal sealed class VectorTemplate(SKPicture picture) : IDisposable
{
    public SKPicture Picture { get; } = picture;
    private static readonly XNamespace Svg = "http://www.w3.org/2000/svg";
    private const int MaximumCharacters = 1_000_000;
    private static readonly string[] CommonAttributes =
        ["fill", "stroke", "stroke-width", "opacity", "fill-opacity", "stroke-opacity", "stroke-linecap", "stroke-linejoin", "clip-path"];

    public static VectorTemplate Load(Stream source)
    {
        using var reader = XmlReader.Create(source, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = MaximumCharacters,
        });
        XElement root;
        try { root = XDocument.Load(reader).Root ?? throw new InvalidDataException("Empty SVG template."); }
        catch (XmlException ex) { throw new InvalidDataException("Invalid SVG template.", ex); }
        if (root.Name != Svg + "svg" || (string?)root.Attribute("viewBox") != "0 0 1024 1024")
            throw new InvalidDataException("Invalid template coordinate system.");
        Validate(root);
        var definitions = root.Descendants().Where(x => x.Attribute("id") is not null)
            .ToDictionary(x => Required(x, "id"), StringComparer.Ordinal);
        using var recorder = new SKPictureRecorder();
        var canvas = recorder.BeginRecording(new SKRect(0, 0, 1024, 1024));
        foreach (var node in root.Elements()) Draw(node, canvas, definitions, new Style());
        return new VectorTemplate(recorder.EndRecording());
    }

    private static void Validate(XElement root)
    {
        var identifiers = new HashSet<string>(StringComparer.Ordinal);
        int count = 0;
        foreach (var node in root.DescendantsAndSelf())
        {
            if (++count > 4096 || node.Name.Namespace != Svg) throw new InvalidDataException("Unsupported SVG structure.");
            string[] attributes = node.Name.LocalName switch
            {
                "svg" when node == root => ["width", "height", "viewBox"],
                "defs" => [],
                "g" => CommonAttributes,
                "path" => [.. CommonAttributes, "d"],
                "rect" => [.. CommonAttributes, "x", "y", "width", "height", "rx", "ry"],
                "ellipse" => [.. CommonAttributes, "cx", "cy", "rx", "ry"],
                "circle" => [.. CommonAttributes, "cx", "cy", "r"],
                "linearGradient" => ["id", "x1", "y1", "x2", "y2", "gradientUnits"],
                "radialGradient" => ["id", "cx", "cy", "r", "gradientUnits"],
                "stop" => ["offset", "stop-color", "stop-opacity"],
                "clipPath" => ["id", "clipPathUnits"],
                _ => throw new InvalidDataException($"Unsupported template element: {node.Name.LocalName}."),
            };
            foreach (var attribute in node.Attributes())
            {
                if (attribute.IsNamespaceDeclaration && node == root && attribute.Value == Svg.NamespaceName) continue;
                if (attribute.Name.Namespace != XNamespace.None || !attributes.Contains(attribute.Name.LocalName, StringComparer.Ordinal))
                    throw new InvalidDataException($"Unsupported template attribute: {attribute.Name.LocalName}.");
            }
            if (node.Attribute("id") is { } id && (!ValidIdentifier(id.Value) || !identifiers.Add(id.Value)))
                throw new InvalidDataException("Invalid or duplicate SVG identifier.");
            if (node.Name.LocalName is "linearGradient" or "radialGradient" or "clipPath")
            {
                if (node.Parent?.Name != Svg + "defs" || node.Attribute("id") is null)
                    throw new InvalidDataException("Template definitions must have identifiers inside defs.");
            }
            if (node.Name == Svg + "defs" && (node.Parent != root || node.Elements().Any(x => x.Name.LocalName is not ("linearGradient" or "radialGradient" or "clipPath"))))
                throw new InvalidDataException("Invalid template definitions.");
            if (node.Name.LocalName is "linearGradient" or "radialGradient")
            {
                if (node.Elements().Any(x => x.Name != Svg + "stop")) throw new InvalidDataException("Invalid gradient stops.");
                if (node.Attribute("gradientUnits") is { } units && units.Value is not ("objectBoundingBox" or "userSpaceOnUse"))
                    throw new InvalidDataException("Unsupported gradient units.");
            }
            if (node.Name == Svg + "clipPath" && (node.Elements().Any(x => x.Name.LocalName is not ("path" or "rect" or "ellipse" or "circle") || x.Attribute("clip-path") is not null)
                || (node.Attribute("clipPathUnits") is { } clipUnits && clipUnits.Value != "userSpaceOnUse")))
                throw new InvalidDataException("Invalid clipping geometry.");
            if (node.Name == Svg + "stop" && node.Parent?.Name.LocalName is not ("linearGradient" or "radialGradient"))
                throw new InvalidDataException("Gradient stops must be inside a gradient.");
            if (node.Name.LocalName is "path" or "rect" or "ellipse" or "circle" or "stop" && node.HasElements)
                throw new InvalidDataException("Shapes and stops cannot contain child elements.");
        }
    }

    private sealed record Style(string Fill = "#000000", string Stroke = "none", float StrokeWidth = 1,
        float FillOpacity = 1, float StrokeOpacity = 1, string Cap = "butt", string Join = "miter");

    private static void Draw(XElement node, SKCanvas canvas, Dictionary<string, XElement> definitions, Style inherited)
    {
        if (node.Name == Svg + "defs") return;
        if (node.Name.LocalName is "linearGradient" or "radialGradient" or "clipPath" or "stop")
            throw new InvalidDataException("Definitions cannot be drawn directly.");
        var style = new Style((string?)node.Attribute("fill") ?? inherited.Fill,
            (string?)node.Attribute("stroke") ?? inherited.Stroke, Nonnegative(node, "stroke-width", inherited.StrokeWidth),
            Unit(node, "fill-opacity", inherited.FillOpacity), Unit(node, "stroke-opacity", inherited.StrokeOpacity),
            (string?)node.Attribute("stroke-linecap") ?? inherited.Cap, (string?)node.Attribute("stroke-linejoin") ?? inherited.Join);
        if (style.Cap is not ("butt" or "round" or "square") || style.Join is not ("miter" or "round" or "bevel"))
            throw new InvalidDataException("Unsupported stroke geometry.");
        int saved = canvas.Save();
        try
        {
            if (node.Attribute("clip-path") is { } clipping)
            {
                var definition = Reference(clipping.Value, definitions);
                if (definition.Name != Svg + "clipPath") throw new InvalidDataException("Expected clipping geometry.");
                using var clip = new SKPath();
                foreach (var child in definition.Elements())
                {
                    using var part = Geometry(child);
                    clip.AddPath(part);
                }
                canvas.ClipPath(clip, SKClipOperation.Intersect, true);
            }
            float opacity = Unit(node, "opacity", 1);
            using var layer = opacity < 1 ? new SKPaint { Color = SKColors.White.WithAlpha(Alpha(opacity)) } : null;
            if (layer is not null) canvas.SaveLayer(layer);
            if (node.Name == Svg + "g")
            {
                foreach (var child in node.Elements()) Draw(child, canvas, definitions, style);
            }
            else
            {
                using var path = Geometry(node);
                PaintPath(canvas, path, style.Fill, style.FillOpacity, false, style, definitions);
                PaintPath(canvas, path, style.Stroke, style.StrokeOpacity, true, style, definitions);
            }
        }
        finally { canvas.RestoreToCount(saved); }
    }

    private static SKPath Geometry(XElement node)
    {
        if (node.Name == Svg + "path")
            return SKPath.ParseSvgPathData(Required(node, "d")) ?? throw new InvalidDataException("Invalid SVG path.");
        var path = new SKPath();
        try
        {
            switch (node.Name.LocalName)
            {
                case "rect":
                    float width = Nonnegative(node, "width"), height = Nonnegative(node, "height");
                    var box = SKRect.Create(Number(node, "x"), Number(node, "y"), width, height);
                    float rx = Nonnegative(node, "rx", Nonnegative(node, "ry"));
                    float ry = Nonnegative(node, "ry", rx);
                    path.AddRoundRect(box, Math.Min(rx, width / 2), Math.Min(ry, height / 2));
                    break;
                case "circle":
                    path.AddCircle(Number(node, "cx"), Number(node, "cy"), Nonnegative(node, "r"));
                    break;
                case "ellipse":
                    float cx = Number(node, "cx"), cy = Number(node, "cy"), ex = Nonnegative(node, "rx"), ey = Nonnegative(node, "ry");
                    path.AddOval(new SKRect(cx - ex, cy - ey, cx + ex, cy + ey));
                    break;
                default: throw new InvalidDataException("Unsupported SVG geometry.");
            }
            return path;
        }
        catch { path.Dispose(); throw; }
    }

    private static void PaintPath(SKCanvas canvas, SKPath path, string value, float opacity, bool stroke, Style style, Dictionary<string, XElement> definitions)
    {
        if (value == "none" || (stroke && style.StrokeWidth == 0)) return;
        using var paint = new SKPaint
        {
            IsAntialias = true, Style = stroke ? SKPaintStyle.Stroke : SKPaintStyle.Fill, StrokeWidth = style.StrokeWidth,
            StrokeCap = style.Cap switch { "round" => SKStrokeCap.Round, "square" => SKStrokeCap.Square, _ => SKStrokeCap.Butt },
            StrokeJoin = style.Join switch { "round" => SKStrokeJoin.Round, "bevel" => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter },
        };
        using var shader = value.StartsWith("url(", StringComparison.Ordinal) ? Gradient(Reference(value, definitions), path.TightBounds) : null;
        paint.Shader = shader;
        paint.Color = shader is null ? Color(value).WithAlpha(Alpha(opacity)) : SKColors.White.WithAlpha(Alpha(opacity));
        canvas.DrawPath(path, paint);
    }

    private static SKShader Gradient(XElement node, SKRect bounds)
    {
        if (node.Name.LocalName is not ("linearGradient" or "radialGradient")) throw new InvalidDataException("Expected a gradient definition.");
        var stops = node.Elements(Svg + "stop").ToArray();
        if (stops.Length < 2) throw new InvalidDataException("A gradient needs at least two stops.");
        var colors = stops.Select(x => Color(Required(x, "stop-color")).WithAlpha(Alpha(Unit(x, "stop-opacity", 1)))).ToArray();
        var positions = stops.Select(x => Unit(x, "offset", 0)).ToArray();
        for (int i = 1; i < positions.Length; i++)
            if (positions[i] < positions[i - 1]) throw new InvalidDataException("Gradient stops must be ordered.");
        bool userSpace = (string?)node.Attribute("gradientUnits") == "userSpaceOnUse";
        var matrix = userSpace ? SKMatrix.Identity : SKMatrix.CreateScaleTranslation(Math.Max(bounds.Width, .001f), Math.Max(bounds.Height, .001f), bounds.Left, bounds.Top);
        if (node.Name == Svg + "linearGradient")
            return SKShader.CreateLinearGradient(new SKPoint(Number(node, "x1"), Number(node, "y1")),
                new SKPoint(Number(node, "x2", 1), Number(node, "y2")), colors, positions, SKShaderTileMode.Clamp, matrix);
        float radius = Nonnegative(node, "r", .5f);
        if (radius == 0) throw new InvalidDataException("A radial gradient needs a positive radius.");
        return SKShader.CreateRadialGradient(new SKPoint(Number(node, "cx", .5f), Number(node, "cy", .5f)), radius,
            colors, positions, SKShaderTileMode.Clamp, matrix);
    }

    private static XElement Reference(string value, Dictionary<string, XElement> definitions)
    {
        if (!value.StartsWith("url(#", StringComparison.Ordinal) || !value.EndsWith(')') || !definitions.TryGetValue(value[5..^1], out var definition))
            throw new InvalidDataException("Only existing internal SVG references are supported.");
        return definition;
    }

    private static bool ValidIdentifier(string id) => id.Length is > 0 and < 80 && char.IsAsciiLetter(id[0])
        && id.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_');
    private static string Required(XElement node, string name) => (string?)node.Attribute(name)
        ?? throw new InvalidDataException($"Missing SVG attribute: {name}.");
    private static SKColor Color(string value) => value.Length == 7 && value[0] == '#' && SKColor.TryParse(value, out var color)
        ? color : throw new InvalidDataException("Template colors must be six-digit RGB values.");
    private static byte Alpha(float value) => (byte)Math.Round(value * 255);
    private static float Number(XElement node, string name, float fallback = 0)
    {
        var text = (string?)node.Attribute(name);
        if (text is null) return fallback;
        bool percent = text.EndsWith('%');
        if (!float.TryParse(percent ? text[..^1] : text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !float.IsFinite(value) || Math.Abs(value) > 8192)
            throw new InvalidDataException($"Invalid SVG number: {name}.");
        return percent ? value / 100 : value;
    }
    private static float Nonnegative(XElement node, string name, float fallback = 0)
    {
        float value = Number(node, name, fallback);
        return value >= 0 ? value : throw new InvalidDataException($"SVG {name} cannot be negative.");
    }
    private static float Unit(XElement node, string name, float fallback)
    {
        float value = Number(node, name, fallback);
        return value is >= 0 and <= 1 ? value : throw new InvalidDataException($"SVG {name} must be between zero and one.");
    }
    public void Dispose() => Picture.Dispose();
}
