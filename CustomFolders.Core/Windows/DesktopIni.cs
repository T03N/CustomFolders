// SPDX-License-Identifier: GPL-3.0-or-later
namespace CustomFolders.PlatformIntegration;

internal static class DesktopIni
{
    private const string OwnerSection = "[CustomFolders.Icon]";
    private const string ApplicationId = "io.github.t03n.customfolders";

    internal static string Update(string existing, string iconName, string? iconHash = null)
    {
        var lines = existing.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
        if (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var result = new List<string>();
        bool inShell = false, foundSection = false, inserted = false;
        foreach (var line in lines)
        {
            string trimmed = line.Trim();
            if (trimmed.StartsWith('[') && trimmed.EndsWith(']'))
            {
                inShell = trimmed.Equals("[.ShellClassInfo]", StringComparison.OrdinalIgnoreCase);
                result.Add(line);
                if (inShell)
                {
                    foundSection = true;
                    if (!inserted) { result.Add($"IconResource={iconName},0"); inserted = true; }
                }
                continue;
            }
            if (inShell && IsIconKey(trimmed)) continue;
            result.Add(line);
        }
        if (!foundSection)
        {
            if (result.Count > 0 && result[^1].Length != 0) result.Add("");
            result.Add("[.ShellClassInfo]");
            result.Add($"IconResource={iconName},0");
        }
        string updated = string.Join("\r\n", result) + "\r\n";
        return iconHash is null ? updated : SetOwnership(updated, iconHash);
    }

    internal static bool OwnsIcon(string existing, string hash) =>
        ReadValue(existing, OwnerSection, "Application") == ApplicationId
        && string.Equals(ReadValue(existing, OwnerSection, "Sha256"), hash, StringComparison.OrdinalIgnoreCase);

    private static string SetOwnership(string existing, string hash)
    {
        var result = new List<string>();
        bool inOwner = false, inserted = false;
        foreach (var line in existing.TrimEnd('\r', '\n').Split("\r\n"))
        {
            var text = line.Trim();
            if (text.StartsWith('[') && text.EndsWith(']'))
            {
                inOwner = text.Equals(OwnerSection, StringComparison.OrdinalIgnoreCase);
                result.Add(line);
                if (inOwner && !inserted)
                {
                    result.Add($"Application={ApplicationId}"); result.Add($"Sha256={hash}"); inserted = true;
                }
            }
            else if (!(inOwner && (text.StartsWith("Application=", StringComparison.OrdinalIgnoreCase) || text.StartsWith("Sha256=", StringComparison.OrdinalIgnoreCase)))) result.Add(line);
        }
        if (!inserted)
        {
            result.Add(""); result.Add(OwnerSection); result.Add($"Application={ApplicationId}"); result.Add($"Sha256={hash}");
        }
        return string.Join("\r\n", result) + "\r\n";
    }

    private static string? ReadValue(string existing, string section, string key)
    {
        bool inSection = false;
        foreach (var line in existing.Replace("\r\n", "\n").Split('\n'))
        {
            var text = line.Trim();
            if (text.StartsWith('[')) inSection = text.Equals(section, StringComparison.OrdinalIgnoreCase);
            else if (inSection && text.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase)) return text[(key.Length + 1)..].Trim();
        }
        return null;
    }

    internal static bool ReferencesIcon(string existing, string iconName)
    {
        bool inShell = false;
        foreach (var line in existing.Replace("\r\n", "\n").Split('\n'))
        {
            var text = line.Trim();
            if (text.StartsWith('[')) inShell = text.Equals("[.ShellClassInfo]", StringComparison.OrdinalIgnoreCase);
            else if (inShell && text.StartsWith("IconResource=", StringComparison.OrdinalIgnoreCase))
                return text[13..].Split(',')[0].Trim().Equals(iconName, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }

    private static bool IsIconKey(string text)
    {
        int separator = text.IndexOf('=');
        if (separator < 0) return false;
        string key = text[..separator].Trim();
        return key.Equals("IconResource", StringComparison.OrdinalIgnoreCase)
            || key.Equals("IconFile", StringComparison.OrdinalIgnoreCase)
            || key.Equals("IconIndex", StringComparison.OrdinalIgnoreCase);
    }
}
