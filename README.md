# CustomFolders

A desktop editor for making folder icons and applying them to Windows folders.

Choose from 16 Windows and macOS folder and drive presets, add an image, emoji, or label, and adjust the live preview. Export a transparent 1024-pixel PNG or apply the icon to a Windows folder.

## Build and run

Use Windows 10 or later and the .NET 10 SDK. Dependencies restore through NuGet.

```sh
dotnet restore CustomFolders.slnx
dotnet run --project CustomFolders
```

Run the tests with `dotnet test CustomFolders.Tests/CustomFolders.Tests.csproj`.

## Use

Import PNG, JPEG, or BMP images. The built-in presets are original SVG drawings; arbitrary SVG import is not supported. The image-search button opens Flaticon in your browser.

Applying an icon creates `.customfolders.ico` and updates `desktop.ini`. Other files and unrelated INI settings are preserved. Existing `.customfolders.ico` files are replaced only when their ownership marker and hash match. Explorer may take a moment to refresh. macOS presets are visual choices; folder application supports Windows only.

## License and credits

Copyright (c) 2026 T03N. Application code, tests, and documentation are **GPL-3.0-or-later**; see [LICENSE](LICENSE). Original folder and drive SVGs and CustomFolders branding are **MIT**; see [LICENSES/MIT.txt](LICENSES/MIT.txt).

All 16 presets are self-contained vector drawings using paths, shapes, gradients, and vector texture. See [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for framework and package credits.

CustomFolders is independent software and is not endorsed by Apple, Microsoft, or Uno Platform.
