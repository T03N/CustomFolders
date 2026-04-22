// SPDX-License-Identifier: GPL-3.0-or-later
using System.Security.AccessControl;
using System.Security.Principal;
using CustomFolders.Models;
using CustomFolders.Rendering;
using CustomFolders.PlatformIntegration;
using SkiaSharp;
using Xunit;

namespace CustomFolders.Tests;

public sealed class FolderApplicationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "CustomFolders-folders-" + Guid.NewGuid().ToString("N"));
    public FolderApplicationTests() => Directory.CreateDirectory(_root);
    public void Dispose()
    {
        foreach (var file in Directory.EnumerateFiles(_root, "*", SearchOption.AllDirectories)) File.SetAttributes(file, FileAttributes.Normal);
        foreach (var folder in Directory.EnumerateDirectories(_root, "*", SearchOption.AllDirectories).Reverse())
            File.SetAttributes(folder, FileAttributes.Directory);
        File.SetAttributes(_root, FileAttributes.Directory);
        Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public void IniUpdatesOnlyTheIconKeysInTheShellSection()
    {
        const string original = "; keep this comment\r\n[Other]\r\nIconResource=other.ico,0\r\n[.shellclassinfo]\r\nInfoTip=Keep my note\r\nIconFile=old.ico\r\nIconIndex=1\r\n[ViewState]\r\nMode=2\r\n";
        string updated = DesktopIni.Update(original, FolderIconApplier.IconFileName);
        Assert.Contains("; keep this comment", updated);
        Assert.Contains("[Other]\r\nIconResource=other.ico,0", updated);
        Assert.Contains("InfoTip=Keep my note", updated);
        Assert.Contains("[ViewState]\r\nMode=2", updated);
        Assert.DoesNotContain("IconFile=", updated);
        Assert.DoesNotContain("IconIndex=", updated);
        Assert.True(DesktopIni.ReferencesIcon(updated, FolderIconApplier.IconFileName));
        Assert.Equal(updated, DesktopIni.Update(updated, FolderIconApplier.IconFileName));
    }

    [Fact]
    public void IcoContainsOneValid256PixelPngFrame()
    {
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        byte[] ico = FolderIconApplier.CreateIco(bitmap);
        using var reader = new BinaryReader(new MemoryStream(ico));
        Assert.Equal(0, reader.ReadUInt16()); Assert.Equal(1, reader.ReadUInt16()); Assert.Equal(1, reader.ReadUInt16());
        Assert.Equal(0, reader.ReadByte()); Assert.Equal(0, reader.ReadByte());
        reader.ReadBytes(6);
        Assert.Equal((uint)(ico.Length - 22), reader.ReadUInt32()); Assert.Equal(22u, reader.ReadUInt32());
        using var frame = SKBitmap.Decode(ico[22..]);
        Assert.Equal(256, frame.Width); Assert.Equal(256, frame.Height);
        Assert.Equal(0, frame.GetPixel(0, 0).Alpha);
    }

    [Theory]
    [InlineData("A folder with spaces")]
    [InlineData("研究 Ω %TEMP% & notes")]
    public async Task ApplyIsRepeatableAndPreservesOtherFiles(string name)
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, name)).FullName;
        await File.WriteAllTextAsync(Path.Combine(folder, "keep.txt"), "keep this file");
        await File.WriteAllTextAsync(Path.Combine(folder, "folder.ico"), "not owned by CustomFolders");
        await File.WriteAllTextAsync(Path.Combine(folder, "desktop.ini"), "[.ShellClassInfo]\r\nInfoTip=My folder\r\n[Other]\r\nValue=7\r\n");
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal("keep this file", await File.ReadAllTextAsync(Path.Combine(folder, "keep.txt")));
        Assert.Equal("not owned by CustomFolders", await File.ReadAllTextAsync(Path.Combine(folder, "folder.ico")));
        string ini = await File.ReadAllTextAsync(Path.Combine(folder, "desktop.ini"));
        Assert.Contains("InfoTip=My folder", ini); Assert.Contains("[Other]\r\nValue=7", ini);
        Assert.True(DesktopIni.ReferencesIcon(ini, FolderIconApplier.IconFileName));
        Assert.True(File.GetAttributes(Path.Combine(folder, "desktop.ini")).HasFlag(FileAttributes.System | FileAttributes.Hidden));
        Assert.True(File.GetAttributes(folder).HasFlag(FileAttributes.ReadOnly | FileAttributes.System));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    [Fact]
    public async Task ReapplyingAnUnchangedIconDoesNotRewriteOpenFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "unchanged icon")).FullName;
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        string icon = Path.Combine(folder, FolderIconApplier.IconFileName);
        string ini = Path.Combine(folder, "desktop.ini");
        DateTime iconModified = File.GetLastWriteTimeUtc(icon), iniModified = File.GetLastWriteTimeUtc(ini);
        using var openIcon = File.Open(icon, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var openIni = File.Open(ini, FileMode.Open, FileAccess.Read, FileShare.Read);
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal(iconModified, File.GetLastWriteTimeUtc(icon));
        Assert.Equal(iniModified, File.GetLastWriteTimeUtc(ini));
    }

    [Fact]
    public async Task UnownedReservedFileIsNotOverwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "collision")).FullName;
        string file = Path.Combine(folder, FolderIconApplier.IconFileName);
        await File.WriteAllTextAsync(file, "leave alone");
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal("leave alone", await File.ReadAllTextAsync(file));
        Assert.False(File.Exists(Path.Combine(folder, "desktop.ini")));
    }

    [Fact]
    public async Task IniReferenceAloneDoesNotGrantOwnershipOfAnExistingIcon()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "referenced collision")).FullName;
        string file = Path.Combine(folder, FolderIconApplier.IconFileName);
        await File.WriteAllTextAsync(file, "third party artwork");
        await File.WriteAllTextAsync(Path.Combine(folder, "desktop.ini"), "[.ShellClassInfo]\r\nIconResource=" + FolderIconApplier.IconFileName + ",0\r\n");
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal("third party artwork", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task UserEditedManagedIconIsNotOverwritten()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "edited icon")).FullName;
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        string file = Path.Combine(folder, FolderIconApplier.IconFileName);
        File.SetAttributes(file, FileAttributes.Normal);
        await File.WriteAllTextAsync(file, "user replacement");
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal("user replacement", await File.ReadAllTextAsync(file));
    }

    [Fact]
    public async Task LockedExistingIniFailsWithoutWritingAnIcon()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "locked")).FullName;
        string ini = Path.Combine(folder, "desktop.ini");
        await File.WriteAllTextAsync(ini, "[Other]\r\nKeep=1\r\n");
        using var locked = File.Open(ini, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.False(File.Exists(Path.Combine(folder, FolderIconApplier.IconFileName)));
    }

    [Fact]
    public async Task FailureAfterIconWriteRollsItBack()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "rollback")).FullName;
        string ini = Path.Combine(folder, "desktop.ini");
        const string original = "[Other]\r\nKeep=1\r\n";
        await File.WriteAllTextAsync(ini, original);
        using var locked = File.Open(ini, FileMode.Open, FileAccess.Read, FileShare.Read);
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.False(File.Exists(Path.Combine(folder, FolderIconApplier.IconFileName)));
        Assert.Equal(original, await File.ReadAllTextAsync(ini));
        Assert.Empty(Directory.EnumerateFiles(folder, "*.tmp"));
    }

    [Fact]
    public async Task IniReplacementPreservesItsAlternateStreamsAndPermissions()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "metadata")).FullName;
        string ini = Path.Combine(folder, "desktop.ini");
        await File.WriteAllTextAsync(ini, "[Other]\r\nKeep=1\r\n");
        await File.WriteAllTextAsync(ini + ":CustomNote", "keep this stream");
        var original = new FileInfo(ini).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access);
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.Null(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal("keep this stream", await File.ReadAllTextAsync(ini + ":CustomNote"));
        Assert.Equal(original, new FileInfo(ini).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.Access));
    }

    [Fact]
    public async Task DeniedCreatePermissionReturnsAnErrorWithoutPartialFiles()
    {
        if (!OperatingSystem.IsWindows()) return;
        var folder = Directory.CreateDirectory(Path.Combine(_root, "permission denied"));
        var original = folder.GetAccessControl();
        var denied = folder.GetAccessControl();
        var user = WindowsIdentity.GetCurrent().User!;
        denied.AddAccessRule(new FileSystemAccessRule(user, FileSystemRights.CreateFiles, AccessControlType.Deny));
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        try
        {
            folder.SetAccessControl(denied);
            Assert.NotNull(await FolderIconApplier.ApplyAsync(folder.FullName, bitmap));
            Assert.Empty(Directory.EnumerateFiles(folder.FullName));
        }
        finally { folder.SetAccessControl(original); }
    }

    [Fact]
    public async Task MissingFolderReturnsAnError()
    {
        if (!OperatingSystem.IsWindows()) return;
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(Path.Combine(_root, "absent"), bitmap));
    }

    [Fact]
    public async Task UnsupportedIniEncodingIsLeftUntouched()
    {
        if (!OperatingSystem.IsWindows()) return;
        string folder = Directory.CreateDirectory(Path.Combine(_root, "legacy encoding")).FullName;
        string ini = Path.Combine(folder, "desktop.ini");
        byte[] original = [0x5B, 0x58, 0x5D, 0x0A, 0xE9, 0x3D, 0x31];
        await File.WriteAllBytesAsync(ini, original);
        using var renderer = new IconRenderer();
        using var bitmap = renderer.Render(new());
        Assert.NotNull(await FolderIconApplier.ApplyAsync(folder, bitmap));
        Assert.Equal(original, await File.ReadAllBytesAsync(ini));
        Assert.False(File.Exists(Path.Combine(folder, FolderIconApplier.IconFileName)));
    }
}
