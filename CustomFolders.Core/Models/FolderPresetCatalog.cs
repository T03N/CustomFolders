// SPDX-License-Identifier: GPL-3.0-or-later
using SkiaSharp;

namespace CustomFolders.Models;

public sealed record FolderPreset(
    FolderVariant Variant, IconType Family, string Label, string ResourceName,
    SKRect OverlayBounds, SKRect LabelBounds, SKColor Accent);

public static class FolderPresetCatalog
{
    private static readonly SKRect FolderOverlay = new(300, 406, 724, 656);
    private static readonly SKRect FolderLabel = new(236, 678, 788, 778);
    private static readonly SKRect MacOverlay = new(284, 396, 740, 692);
    private static readonly SKRect MacLabel = new(236, 718, 788, 826);
    private static readonly SKRect DriveOverlay = new(348, 348, 676, 570);
    private static readonly SKRect DriveLabel = new(266, 626, 758, 718);

    private static FolderPreset Folder(FolderVariant variant, IconType family, string label, string file, string accent)
        => new(variant, family, label, $"CustomFolders.Templates.{file}.svg",
            family == IconType.Windows11 ? FolderOverlay : MacOverlay,
            family == IconType.Windows11 ? FolderLabel : MacLabel, SKColor.Parse(accent));

    private static FolderPreset Drive(FolderVariant variant, IconType family, string label, string file, string accent)
        => new(variant, family, label, $"CustomFolders.Templates.{file}.svg", DriveOverlay, DriveLabel, SKColor.Parse(accent));

    public static IReadOnlyList<FolderPreset> All { get; } = Array.AsReadOnly(new[]
    {
        Folder(FolderVariant.Windows11Default, IconType.Windows11, "Default", "windows-amber", "#68450D"),
        Folder(FolderVariant.Windows11Pink, IconType.Windows11, "Pink", "windows-rose", "#762C4C"),
        Folder(FolderVariant.MacOsDefaultDark, IconType.MacOs, "Default Dark", "mac-ocean", "#143E60"),
        Folder(FolderVariant.MacOsDefaultLight, IconType.MacOs, "Default Light", "mac-sky", "#1C526D"),
        Folder(FolderVariant.MacOsGreen, IconType.MacOs, "Green", "mac-green", "#134E3C"),
        Folder(FolderVariant.MacOsLime, IconType.MacOs, "Lime", "mac-lime", "#354B12"),
        Folder(FolderVariant.MacOsYellow, IconType.MacOs, "Yellow", "mac-yellow", "#624C0E"),
        Folder(FolderVariant.MacOsOrange, IconType.MacOs, "Orange", "mac-orange", "#663216"),
        Folder(FolderVariant.MacOsRed, IconType.MacOs, "Red", "mac-red", "#6D2632"),
        Folder(FolderVariant.MacOsPurple, IconType.MacOs, "Purple", "mac-violet", "#422D70"),
        Folder(FolderVariant.MacOsGray, IconType.MacOs, "Gray", "mac-slate", "#233C4C"),
        Folder(FolderVariant.MacOsBlack, IconType.MacOs, "Black", "mac-graphite", "#DFE9F0"),
        Drive(FolderVariant.MacOsHdRemovable, IconType.MacOsHd, "Removable Drive", "drive-removable", "#253F53"),
        Drive(FolderVariant.MacOsHdExternal, IconType.MacOsHd, "External Drive", "drive-external", "#213C52"),
        Drive(FolderVariant.MacOsTimeMachine, IconType.MacOsFlatDrive, "Backup Drive", "drive-backup", "#C7EDE1"),
        Drive(FolderVariant.MacOsFileServer, IconType.MacOsFlatDrive, "File Server", "drive-server", "#D5EAF7"),
    });

    private static readonly IReadOnlyDictionary<FolderVariant, FolderPreset> ByVariant = All.ToDictionary(x => x.Variant);
    public static IReadOnlyList<FolderPreset> Windows { get; } = Array.AsReadOnly(All.Where(x => x.Family == IconType.Windows11).ToArray());
    public static IReadOnlyList<FolderPreset> Mac { get; } = Array.AsReadOnly(All.Where(x => x.Family != IconType.Windows11).ToArray());

    public static FolderPreset Get(FolderVariant variant) => ByVariant.TryGetValue(variant, out var preset)
        ? preset : throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown folder preset.");
}
