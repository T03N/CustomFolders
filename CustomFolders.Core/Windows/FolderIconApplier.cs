// SPDX-License-Identifier: GPL-3.0-or-later
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using SkiaSharp;

namespace CustomFolders.PlatformIntegration;

public static class FolderIconApplier
{
    public const string IconFileName = ".customfolders.ico";
    private static readonly SemaphoreSlim WriteGate = new(1, 1);

    public static async Task<string?> ApplyAsync(string folderPath, SKBitmap bitmap)
    {
        ArgumentNullException.ThrowIfNull(bitmap);
        if (!OperatingSystem.IsWindows()) return "Applying folder icons is supported on Windows only.";
        await WriteGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
                return "The selected folder is no longer available.";
            string folder = Path.GetFullPath(folderPath);
            string iconPath = Path.Combine(folder, IconFileName);
            string iniPath = Path.Combine(folder, "desktop.ini");
            var originalAttributes = File.GetAttributes(folder);
            var icon = Snapshot.Capture(iconPath);
            var ini = Snapshot.Capture(iniPath);
            string oldText = ini.Content is null ? "" : ReadText(ini.Content);
            if (icon.Content is not null && (!DesktopIni.ReferencesIcon(oldText, IconFileName)
                || !DesktopIni.OwnsIcon(oldText, Convert.ToHexString(SHA256.HashData(icon.Content)))))
                return $"A file named {IconFileName} already exists and is not managed by this app. No files were changed.";

            byte[] iconBytes = CreateIco(bitmap);
            string iconHash = Convert.ToHexString(SHA256.HashData(iconBytes));
            byte[] iniBytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(DesktopIni.Update(oldText, IconFileName, iconHash))).ToArray();
            bool sameIcon = icon.Content is not null && icon.Content.AsSpan().SequenceEqual(iconBytes);
            bool sameIni = ini.Content is not null && ini.Content.AsSpan().SequenceEqual(iniBytes);
            const FileAttributes folderFlags = FileAttributes.ReadOnly | FileAttributes.System;
            const FileAttributes iniFlags = FileAttributes.Hidden | FileAttributes.System;
            if (sameIcon && sameIni && (icon.Attributes & FileAttributes.Hidden) != 0
                && (ini.Attributes & iniFlags) == iniFlags && (originalAttributes & folderFlags) == folderFlags)
                return null;
            bool changed = false;
            try
            {
                changed = true;
                if (!sameIcon) await WriteAtomicAsync(iconPath, iconBytes).ConfigureAwait(false);
                if (!sameIni) await WriteAtomicAsync(iniPath, iniBytes).ConfigureAwait(false);
                File.SetAttributes(iconPath, File.GetAttributes(iconPath) | icon.Attributes | FileAttributes.Hidden);
                File.SetAttributes(iniPath, File.GetAttributes(iniPath) | ini.Attributes | FileAttributes.Hidden | FileAttributes.System);
                File.SetAttributes(folder, originalAttributes | FileAttributes.ReadOnly | FileAttributes.System);
            }
            catch
            {
                if (changed)
                {
                    bool restored = icon.TryRestore() & ini.TryRestore();
                    try { File.SetAttributes(folder, originalAttributes); } catch { restored = false; }
                    if (!restored) throw new IOException("The icon could not be applied and some earlier file changes could not be restored.");
                }
                throw;
            }
            NativeMethods.SHChangeNotify(0x00002000, 0x0005, folder, IntPtr.Zero);
            NativeMethods.SHChangeNotify(0x00001000, 0x0005, folder, IntPtr.Zero);
            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return $"Could not apply the folder icon: {ex.Message}";
        }
        finally { WriteGate.Release(); }
    }

    internal static byte[] CreateIco(SKBitmap bitmap)
    {
        using var small = bitmap.Resize(new SKImageInfo(256, 256), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear))
            ?? throw new InvalidOperationException("Could not resize the icon.");
        using var image = SKImage.FromBitmap(small);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100)
            ?? throw new InvalidOperationException("Could not encode the icon.");
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)1);
        writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0); writer.Write((byte)0);
        writer.Write((ushort)1); writer.Write((ushort)32);
        writer.Write(checked((uint)png.Size)); writer.Write((uint)22);
        writer.Write(png.ToArray());
        return stream.ToArray();
    }

    private static string ReadText(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var reader = new StreamReader(input, new UTF8Encoding(false, throwOnInvalidBytes: true), detectEncodingFromByteOrderMarks: true);
        return reader.ReadToEnd();
    }

    private static async Task WriteAtomicAsync(string path, byte[] bytes)
    {
        string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes).ConfigureAwait(false);
            MakeWritable(path);
            if (File.Exists(path)) File.Replace(temporary, path, destinationBackupFileName: null, ignoreMetadataErrors: false);
            else File.Move(temporary, path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void MakeWritable(string path)
    {
        if (File.Exists(path)) File.SetAttributes(path, File.GetAttributes(path)
            & ~(FileAttributes.ReadOnly | FileAttributes.Hidden | FileAttributes.System));
    }

    private sealed record Snapshot(string Path, byte[]? Content, FileAttributes Attributes)
    {
        public static Snapshot Capture(string path) => File.Exists(path)
            ? new(path, File.ReadAllBytes(path), File.GetAttributes(path)) : new(path, null, FileAttributes.Normal);
        public bool TryRestore()
        {
            try
            {
                if (Content is null) { if (File.Exists(Path)) File.Delete(Path); }
                else
                {
                    if (!File.Exists(Path) || !File.ReadAllBytes(Path).AsSpan().SequenceEqual(Content))
                    {
                        MakeWritable(Path);
                        File.WriteAllBytes(Path, Content);
                    }
                    if (File.GetAttributes(Path) != Attributes) File.SetAttributes(Path, Attributes);
                }
                return true;
            }
            catch { return false; }
        }
    }

    private static class NativeMethods
    {
        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        internal static extern void SHChangeNotify(uint eventId, uint flags, string item1, IntPtr item2);
    }
}
