// SPDX-License-Identifier: GPL-3.0-or-later
using CustomFolders.Models;
using CustomFolders.Services;
using Microsoft.UI.Xaml;
using SkiaSharp;
using Uno.WinUI.Graphics2DSK;

namespace CustomFolders.Presentation;

public sealed class FolderPreviewControl : SKCanvasElement
{
    public static readonly DependencyProperty ConfigProperty = DependencyProperty.Register(
        nameof(Config), typeof(FolderIconConfig), typeof(FolderPreviewControl), new PropertyMetadata(null, Changed));
    public static readonly DependencyProperty CustomIconPathProperty = DependencyProperty.Register(
        nameof(CustomIconPath), typeof(string), typeof(FolderPreviewControl), new PropertyMetadata(null, Changed));

    public FolderIconConfig? Config { get => (FolderIconConfig?)GetValue(ConfigProperty); set => SetValue(ConfigProperty, value); }
    public string? CustomIconPath { get => (string?)GetValue(CustomIconPathProperty); set => SetValue(CustomIconPathProperty, value); }
    public string? LastError { get; private set; }

    private static void Changed(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        => ((FolderPreviewControl)sender).Invalidate();

    protected override void RenderOverride(SKCanvas canvas, Windows.Foundation.Size area)
    {
        canvas.Clear(new SKColor(235, 240, 244));
        using var tile = new SKPaint { Color = new SKColor(219, 227, 234) };
        const int step = 20;
        for (int row = 0; row * step < area.Height; row++)
            for (int col = 0; col * step < area.Width; col++)
                if ((row + col) % 2 == 0) canvas.DrawRect(col * step, row * step, step, step, tile);
        if (Config is null) return;
        try
        {
            FolderIconService.DrawPreview(canvas, area, Config, CustomIconPath);
            LastError = null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or InvalidOperationException)
        {
            LastError = ex.Message;
            using var paint = new SKPaint { Color = new SKColor(135, 45, 45), IsAntialias = true };
            using var font = new SKFont(SKTypeface.Default, 15);
            canvas.DrawText("Image unavailable — pick another file.", 20, 32, SKTextAlign.Left, font, paint);
        }
    }
}
