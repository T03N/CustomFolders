// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using SkiaSharp;

namespace CustomFolders.Rendering;

public sealed partial class IconRenderer
{
    private readonly Dictionary<string, CachedImage> _images = new(StringComparer.OrdinalIgnoreCase);

    private sealed class CachedImage(SKImage image, long length, DateTime modified) : IDisposable
    {
        public SKImage Image { get; } = image;
        public long Length { get; } = length;
        public DateTime Modified { get; } = modified;
        public void Dispose() => Image.Dispose();
    }

    private void DrawImageOverlay(SKCanvas canvas, FolderPreset preset, FolderIconConfig config, string? customIconPath)
    {
        var path = customIconPath ?? config.CustomIconPath;
        if (string.IsNullOrWhiteSpace(path)) return;
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("The selected image is no longer available.");
        if (!_images.TryGetValue(file.FullName, out var cached)
            || cached.Length != file.Length || cached.Modified != file.LastWriteTimeUtc)
        {
            // Keep at most one imported image; selecting another releases the previous one.
            foreach (var entry in _images.Values) { _owned.Remove(entry); entry.Dispose(); }
            _images.Clear();
            using var bitmap = SKBitmap.Decode(file.FullName)
                ?? throw new InvalidDataException("The selected file is not a supported raster image.");
            cached = new CachedImage(SKImage.FromBitmap(bitmap), file.Length, file.LastWriteTimeUtc);
            _images.Add(file.FullName, cached);
            _owned.Add(cached);
        }
        var bounds = preset.OverlayBounds;
        var fit = Math.Min(bounds.Width / cached.Image.Width, bounds.Height / cached.Image.Height) * (float)config.ScaleFactor;
        var width = cached.Image.Width * fit;
        var height = cached.Image.Height * fit;
        var cx = bounds.MidX + (float)config.IconOffsetX;
        var cy = bounds.MidY + (float)config.IconOffsetY;
        var destination = new SKRect(cx - width / 2, cy - height / 2, cx + width / 2, cy + height / 2);
        using var paint = new SKPaint { IsAntialias = true };
        using var tint = config.AdjustColor ? SKColorFilter.CreateBlendMode(preset.Accent, SKBlendMode.SrcIn) : null;
        paint.ColorFilter = tint;
        canvas.DrawImage(cached.Image, destination, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
    }
}
