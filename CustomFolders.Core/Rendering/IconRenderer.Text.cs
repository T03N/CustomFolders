// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using SkiaSharp;
using System.Text;

namespace CustomFolders.Rendering;

public sealed partial class IconRenderer
{
    private void DrawTextOverlays(SKCanvas canvas, FolderPreset preset, FolderIconConfig config, string? customIconPath)
    {
        if (string.IsNullOrWhiteSpace(customIconPath ?? config.CustomIconPath) && !string.IsNullOrWhiteSpace(config.EmojiIcon))
        {
            var box = preset.OverlayBounds;
            var factor = (float)config.ScaleFactor;
            box = SKRect.Create(box.MidX - box.Width * factor / 2 + (float)config.IconOffsetX,
                box.MidY - box.Height * factor / 2 + (float)config.IconOffsetY, box.Width * factor, box.Height * factor);
            DrawFittedText(canvas, config.EmojiIcon, box, box.Height * .82f, preset.Accent, "Segoe UI Emoji");
        }
        if (!string.IsNullOrWhiteSpace(config.Text))
        {
            var box = preset.LabelBounds;
            box.Offset(0, (float)config.TextOffsetY);
            DrawFittedText(canvas, config.Text, box, config.TextSize, preset.Accent, "Segoe UI");
        }
    }

    private static void DrawFittedText(SKCanvas canvas, string text, SKRect box, float requestedSize, SKColor color, string family)
    {
        using var typeface = SKTypeface.FromFamilyName(family);
        using var font = new SKFont(typeface ?? SKTypeface.Default, Math.Min(requestedSize, box.Height));
        using var fallback = FindFallback(font, family, text);
        if (fallback is not null) font.Typeface = fallback;
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        SKRect measured = default;
        float low = 1, high = font.Size;
        for (int i = 0; i < 12; i++)
        {
            font.Size = (low + high) / 2;
            font.MeasureText(text, out measured, paint);
            if (measured.Width <= box.Width && measured.Height <= box.Height) low = font.Size;
            else high = font.Size;
        }
        font.Size = low;
        font.MeasureText(text, out measured, paint);
        canvas.Save();
        try
        {
            canvas.ClipRect(box);
            canvas.DrawText(text, box.MidX - measured.MidX, box.MidY - measured.MidY, SKTextAlign.Left, font, paint);
        }
        finally { canvas.Restore(); }
    }

    private static SKTypeface? FindFallback(SKFont font, string family, string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value is 0x200D or 0xFE0E or 0xFE0F || font.ContainsGlyph(rune.Value)) continue;
            return SKFontManager.Default.MatchCharacter(family, rune.Value);
        }
        return null;
    }
}
