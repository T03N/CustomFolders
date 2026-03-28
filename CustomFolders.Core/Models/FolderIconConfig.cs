// SPDX-License-Identifier: GPL-3.0-or-later
namespace CustomFolders.Models;

public enum IconType { Windows11, MacOs, MacOsHd, MacOsFlatDrive }

// Existing identifiers stay stable for callers; the artwork is independently authored.
public enum FolderVariant
{
    Windows11Default, Windows11Pink,
    MacOsDefaultDark, MacOsDefaultLight, MacOsGreen, MacOsLime, MacOsYellow,
    MacOsOrange, MacOsRed, MacOsPurple, MacOsGray, MacOsBlack,
    MacOsHdRemovable, MacOsHdExternal, MacOsTimeMachine, MacOsFileServer
}

public sealed class FolderIconConfig
{
    public IconType IconType { get; set; } = IconType.Windows11;
    public FolderVariant Variant { get; set; } = FolderVariant.Windows11Default;
    public bool AdjustColor { get; set; } = true;
    public string Text { get; set; } = "";
    public string EmojiIcon { get; set; } = "";
    public string? CustomIconPath { get; set; }
    public double ScaleFactor { get; set; } = 1;
    public double IconOffsetX { get; set; }
    public double IconOffsetY { get; set; }
    public double TextOffsetY { get; set; }
    public float TextSize { get; set; } = 118;
}
