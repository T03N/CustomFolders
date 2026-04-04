// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using SkiaSharp;

namespace CustomFolders.Rendering;

/// <summary>Composes embedded SVG presets and user content on a shared canvas.</summary>
public sealed partial class IconRenderer : IDisposable
{
    public const int CanvasSize = 1024;
    private readonly object _gate = new();
    private readonly Dictionary<FolderVariant, VectorTemplate> _templates = new();
    private readonly List<IDisposable> _owned = new();
    private bool _disposed;

    public SKBitmap Render(FolderIconConfig config, string? customIconPath = null, int size = CanvasSize)
    {
        if (size < 1 || size > 4096) throw new ArgumentOutOfRangeException(nameof(size));
        var bitmap = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        try
        {
            using var canvas = new SKCanvas(bitmap);
            canvas.Clear(SKColors.Transparent);
            Draw(canvas, new SKRect(0, 0, size, size), config, customIconPath);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public void Draw(SKCanvas canvas, SKRect viewport, FolderIconConfig config, string? customIconPath = null)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(config);
        if (viewport.Width <= 0 || viewport.Height <= 0) return;
        Validate(config);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var preset = FolderPresetCatalog.Get(config.Variant);
            if (!_templates.TryGetValue(preset.Variant, out var svg))
            {
                using var source = typeof(IconRenderer).Assembly.GetManifestResourceStream(preset.ResourceName)
                    ?? throw new InvalidOperationException($"Missing embedded preset: {preset.Label}.");
                svg = VectorTemplate.Load(source);
                _templates.Add(preset.Variant, svg);
                _owned.Add(svg);
            }
            var scale = Math.Min(viewport.Width, viewport.Height) / CanvasSize;
            canvas.Save();
            try
            {
                canvas.Translate(viewport.MidX - CanvasSize * scale / 2, viewport.MidY - CanvasSize * scale / 2);
                canvas.Scale(scale);
                canvas.ClipRect(new SKRect(0, 0, CanvasSize, CanvasSize));
                canvas.DrawPicture(svg.Picture);
            }
            finally { canvas.Restore(); }
        }
    }

    public byte[] RenderPng(FolderIconConfig config, string? customIconPath = null, int size = CanvasSize)
    {
        using var bitmap = Render(config, customIconPath, size);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode the icon.");
        return data.ToArray();
    }

    private static void Validate(FolderIconConfig config)
    {
        if (!double.IsFinite(config.ScaleFactor) || config.ScaleFactor <= 0 || config.ScaleFactor > 8
            || !double.IsFinite(config.IconOffsetX) || !double.IsFinite(config.IconOffsetY)
            || !double.IsFinite(config.TextOffsetY) || !float.IsFinite(config.TextSize) || config.TextSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(config), "Icon dimensions must be finite and positive.");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed) return;
            _disposed = true;
            foreach (var item in _owned) item.Dispose();
            _owned.Clear();
            _templates.Clear();
        }
    }
}
