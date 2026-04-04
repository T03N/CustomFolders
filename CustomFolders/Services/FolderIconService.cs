// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using CustomFolders.Rendering;
using SkiaSharp;

namespace CustomFolders.Services;

public static class FolderIconService
{
    private static readonly IconRenderer Renderer = new();

    public static SKBitmap RenderIcon(FolderIconConfig config, string? customIconPath = null)
        => Renderer.Render(config, customIconPath);

    public static void DrawPreview(SKCanvas canvas, Windows.Foundation.Size area, FolderIconConfig config, string? customIconPath)
        => Renderer.Draw(canvas, new SKRect(0, 0, (float)area.Width, (float)area.Height), config, customIconPath);

    public static void Dispose() => Renderer.Dispose();
}
