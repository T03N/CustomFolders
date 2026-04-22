// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using CustomFolders.Rendering;
using System.Text;
using System.Xml.Linq;
using SkiaSharp;
using Xunit;

namespace CustomFolders.Tests;

public sealed class RendererTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "CustomFolders-tests-" + Guid.NewGuid().ToString("N"));

    public RendererTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    public static IEnumerable<object[]> PresetSizes => FolderPresetCatalog.All.SelectMany(preset =>
        new[] { 16, 32, 64, 256, 1024 }.Select(size => new object[] { preset.Variant, size }));

    [Theory]
    [MemberData(nameof(PresetSizes))]
    public void EveryPresetRendersWithTransparentCorners(FolderVariant variant, int size)
    {
        using var renderer = new IconRenderer();
        using var icon = renderer.Render(new FolderIconConfig { Variant = variant }, size: size);
        Assert.Equal(size, icon.Width);
        Assert.Equal(size, icon.Height);
        Assert.Equal(0, icon.GetPixel(0, 0).Alpha);
        Assert.Equal(0, icon.GetPixel(size - 1, size - 1).Alpha);
        Assert.Contains(icon.Pixels, pixel => pixel.Alpha > 200);
    }

    [Fact]
    public void CatalogHasSixteenUniqueAvailableResources()
    {
        Assert.Equal(16, FolderPresetCatalog.All.Count);
        Assert.Equal(16, FolderPresetCatalog.All.Select(x => x.ResourceName).Distinct().Count());
        var resources = typeof(IconRenderer).Assembly.GetManifestResourceNames();
        foreach (var preset in FolderPresetCatalog.All) Assert.Contains(preset.ResourceName, resources);
        Assert.DoesNotContain(FolderPresetCatalog.All, x => x.Label.Contains("Time Machine", StringComparison.Ordinal));
    }

    [Fact]
    public void PresetsContainOnlyOriginalVectorElements()
    {
        XNamespace svg = "http://www.w3.org/2000/svg";
        foreach (var preset in FolderPresetCatalog.All)
        {
            using var source = typeof(IconRenderer).Assembly.GetManifestResourceStream(preset.ResourceName)!;
            using var reader = new StreamReader(source);
            string text = reader.ReadToEnd();
            var root = XDocument.Parse(text).Root!;
            Assert.Equal("0 0 1024 1024", (string?)root.Attribute("viewBox"));
            Assert.Contains("SPDX-License-Identifier: MIT", text);
            Assert.Empty(root.Descendants(svg + "image"));
            Assert.DoesNotContain(root.DescendantsAndSelf().Attributes(), attribute => attribute.Name.LocalName is "href" or "src");
            Assert.NotEmpty(root.Descendants(svg + "path"));
        }
    }

    [Theory]
    [InlineData("<image/>")]
    [InlineData("<script/>")]
    [InlineData("<foreignObject/>")]
    [InlineData("<use href=\"https://example.invalid/shape.svg\"/>")]
    [InlineData("<text>Not supported</text>")]
    [InlineData("<rect width=\"30\" height=\"30\" fill=\"url(https://example.invalid/color)\"/>")]
    [InlineData("<rect width=\"30\" height=\"30\" fill=\"url(#missing)\"/>")]
    public void TemplateLoaderRejectsUnsupportedElementsAndReferences(string body)
    {
        using var source = TemplateStream(body);
        Assert.Throws<InvalidDataException>(() => VectorTemplate.Load(source));
    }

    [Theory]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("-1")]
    public void TemplateLoaderRejectsInvalidDimensions(string width)
    {
        using var source = TemplateStream($"<rect width=\"{width}\" height=\"10\"/>");
        Assert.Throws<InvalidDataException>(() => VectorTemplate.Load(source));
    }

    [Fact]
    public void TemplateLoaderRejectsDuplicateIdentifiers()
    {
        using var source = TemplateStream("<defs><clipPath id=\"clip\"><rect width=\"10\" height=\"10\"/></clipPath><clipPath id=\"clip\"><rect width=\"20\" height=\"20\"/></clipPath></defs>");
        Assert.Throws<InvalidDataException>(() => VectorTemplate.Load(source));
    }

    [Fact]
    public void TemplateLoaderRejectsDtdAndWrongCoordinates()
    {
        const string root = "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1024 1024\"/>";
        using var dtd = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE svg SYSTEM \"https://example.invalid/template.dtd\">" + root));
        Assert.Throws<InvalidDataException>(() => VectorTemplate.Load(dtd));
        using var wrong = new MemoryStream(Encoding.UTF8.GetBytes(root.Replace("1024 1024", "512 512")));
        Assert.Throws<InvalidDataException>(() => VectorTemplate.Load(wrong));
    }

    [Fact]
    public void InternalClipLimitsGradientPainting()
    {
        using var icon = RenderTemplate("<defs><clipPath id=\"clip\"><rect x=\"100\" y=\"100\" width=\"200\" height=\"200\"/></clipPath><linearGradient id=\"color\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#FF0000\"/><stop offset=\"1\" stop-color=\"#0000FF\"/></linearGradient></defs><g clip-path=\"url(#clip)\"><rect width=\"1024\" height=\"1024\" fill=\"url(#color)\"/></g>");
        Assert.Equal(0, icon.GetPixel(99, 200).Alpha);
        Assert.Equal(0, icon.GetPixel(300, 200).Alpha);
        Assert.Equal(255, icon.GetPixel(200, 200).Alpha);
        Assert.True(icon.GetPixel(200, 200).Red > icon.GetPixel(200, 200).Blue);
    }

    [Fact]
    public void GradientsUseEachShapesBoundsAndPreserveTransparency()
    {
        using var icon = RenderTemplate("<defs><linearGradient id=\"color\" x1=\"0\" y1=\"0\" x2=\"1\" y2=\"0\"><stop offset=\"0\" stop-color=\"#FF0000\"/><stop offset=\"1\" stop-color=\"#0000FF\"/></linearGradient></defs><rect x=\"200\" y=\"200\" width=\"400\" height=\"200\" fill=\"url(#color)\"/>");
        Assert.True(icon.GetPixel(220, 250).Red > 230);
        Assert.True(icon.GetPixel(580, 250).Blue > 230);
        Assert.InRange(icon.GetPixel(400, 250).Red, 120, 135);
        Assert.Equal(0, icon.GetPixel(100, 250).Alpha);
    }

    [Fact]
    public void GroupOpacityAppliesToTheCompositedGroup()
    {
        using var icon = RenderTemplate("<g opacity=\".5\"><rect x=\"100\" y=\"100\" width=\"300\" height=\"300\" fill=\"#FF0000\"/><rect x=\"200\" y=\"100\" width=\"300\" height=\"300\" fill=\"#0000FF\"/></g>");
        Assert.InRange(icon.GetPixel(300, 250).Alpha, 127, 128);
        Assert.Equal(0, icon.GetPixel(300, 250).Red);
        Assert.Equal(255, icon.GetPixel(300, 250).Blue);
    }

    [Fact]
    public void RadialGradientsFollowEllipticalShapeBounds()
    {
        using var icon = RenderTemplate("<defs><radialGradient id=\"glow\"><stop offset=\"0\" stop-color=\"#FFFFFF\"/><stop offset=\"1\" stop-color=\"#FFFFFF\" stop-opacity=\"0\"/></radialGradient></defs><ellipse cx=\"500\" cy=\"500\" rx=\"300\" ry=\"100\" fill=\"url(#glow)\"/>");
        Assert.True(icon.GetPixel(500, 500).Alpha > 250);
        Assert.InRange(Math.Abs(icon.GetPixel(650, 500).Alpha - icon.GetPixel(500, 550).Alpha), 0, 3);
        Assert.Equal(0, icon.GetPixel(500, 650).Alpha);
    }

    private static MemoryStream TemplateStream(string body) => new(Encoding.UTF8.GetBytes(
        "<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 1024 1024\">" + body + "</svg>"));

    private static SKBitmap RenderTemplate(string body)
    {
        using var source = TemplateStream(body);
        using var template = VectorTemplate.Load(source);
        var bitmap = new SKBitmap(1024, 1024, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawPicture(template.Picture);
        return bitmap;
    }

    [Fact]
    public void PreviewAndExportUseTheSameComposition()
    {
        using var renderer = new IconRenderer();
        var config = new FolderIconConfig { Variant = FolderVariant.MacOsPurple, Text = "Documents", EmojiIcon = "★" };
        using var rendered = renderer.Render(config);
        using var preview = new SKBitmap(1024, 1024, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(preview);
        canvas.Clear(SKColors.Transparent);
        renderer.Draw(canvas, new SKRect(0, 0, 1024, 1024), config);
        Assert.Equal(rendered.Bytes, preview.Bytes);
        using var decoded = SKBitmap.Decode(renderer.RenderPng(config));
        Assert.Equal(rendered.Pixels, decoded.Pixels);
    }

    [Fact]
    public void SupportedSymbolsDoNotRenderAsMissingGlyphBoxes()
    {
        using var renderer = new IconRenderer();
        using var symbol = renderer.Render(new FolderIconConfig { EmojiIcon = "★" });
        using var unavailable = renderer.Render(new FolderIconConfig { EmojiIcon = "\u0378" });
        Assert.False(symbol.Bytes.SequenceEqual(unavailable.Bytes));
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(140, 30)]
    [InlineData(30, 140)]
    public void CustomImagesFitPresetsAndPreserveTheirOriginalColors(int width, int height)
    {
        var path = SaveImage("overlay.png", width, height, new SKColor(220, 20, 40));
        using var renderer = new IconRenderer();
        foreach (var preset in FolderPresetCatalog.All)
        {
            var config = new FolderIconConfig { Variant = preset.Variant, AdjustColor = false };
            using var icon = renderer.Render(config, path);
            var point = preset.OverlayBounds;
            Assert.Equal(new SKColor(220, 20, 40), icon.GetPixel((int)point.MidX, (int)point.MidY));
        }
    }

    [Fact]
    public void TintPreservesTheAlphaOfTranslucentPixels()
    {
        var path = SaveImage("translucent.png", 60, 60, new SKColor(255, 0, 0, 60));
        using var renderer = new IconRenderer();
        var preset = FolderPresetCatalog.Get(FolderVariant.Windows11Default);
        using var plain = renderer.Render(new FolderIconConfig());
        using var tinted = renderer.Render(new FolderIconConfig(), path);
        int x = (int)preset.OverlayBounds.MidX, y = (int)preset.OverlayBounds.MidY;
        var background = plain.GetPixel(x, y);
        var result = tinted.GetPixel(x, y);
        Assert.NotEqual(background, result);
        Assert.NotEqual(preset.Accent, result);
    }

    [Fact]
    public void ReplacingAnImageAtTheSamePathInvalidatesTheCache()
    {
        var path = SaveImage("same.png", 30, 30, SKColors.Red);
        using var renderer = new IconRenderer();
        var config = new FolderIconConfig { AdjustColor = false };
        using var first = renderer.Render(config, path);
        SaveImage("same.png", 40, 40, SKColors.Blue);
        File.SetLastWriteTimeUtc(path, DateTime.UtcNow.AddSeconds(3));
        using var second = renderer.Render(config, path);
        var center = FolderPresetCatalog.Get(config.Variant).OverlayBounds;
        Assert.Equal(SKColors.Red, first.GetPixel((int)center.MidX, (int)center.MidY));
        Assert.Equal(SKColors.Blue, second.GetPixel((int)center.MidX, (int)center.MidY));
    }

    [Fact]
    public void MissingAndInvalidImagesFailClearly()
    {
        using var renderer = new IconRenderer();
        Assert.Throws<FileNotFoundException>(() => renderer.Render(new(), Path.Combine(_directory, "missing.png")));
        var bad = Path.Combine(_directory, "bad.png");
        File.WriteAllText(bad, "not an image");
        Assert.Throws<InvalidDataException>(() => renderer.Render(new(), bad));
    }

    [Theory]
    [InlineData(.5, -100, -100)]
    [InlineData(2, 100, 100)]
    public void LabelsEmojiAndControlExtremesRender(double scale, double iconY, double textY)
    {
        using var renderer = new IconRenderer();
        foreach (var preset in FolderPresetCatalog.All)
        {
            using var icon = renderer.Render(new FolderIconConfig
            {
                Variant = preset.Variant, Text = new string('W', 160), EmojiIcon = "📁", ScaleFactor = scale,
                IconOffsetY = iconY, TextOffsetY = textY,
            });
            Assert.Contains(icon.Pixels, p => p.Alpha > 0);
        }
    }

    [Fact]
    public void InvalidDimensionsAndDisposedRendererAreRejected()
    {
        using var renderer = new IconRenderer();
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Render(new(), size: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Render(new() { ScaleFactor = double.NaN }));
        Assert.Throws<ArgumentOutOfRangeException>(() => renderer.Render(new() { Variant = (FolderVariant)999 }));
        renderer.Dispose();
        Assert.Throws<ObjectDisposedException>(() => renderer.Render(new()));
    }

    private string SaveImage(string name, int width, int height, SKColor color)
    {
        string path = Path.Combine(_directory, name);
        using var bitmap = new SKBitmap(width, height);
        bitmap.Erase(color);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        File.WriteAllBytes(path, data.ToArray());
        return path;
    }
}
