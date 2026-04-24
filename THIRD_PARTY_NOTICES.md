# Third-party notices

## Source scaffolding

Uno application, resource, and manifest scaffolding is based on the Apache-2.0 Uno templates, Copyright Uno Platform / nventive. Application startup, metadata, and editor integration include CustomFolders modifications. See [LICENSES/Apache-2.0.txt](LICENSES/Apache-2.0.txt).

## Package dependencies

Packages are restored through NuGet; their source and runtime binaries are not vendored in this repository. Their licenses remain separate from CustomFolders' license.

| Component | License and upstream notices |
| --- | --- |
| Uno Platform / Graphics2DSK / Diagnostics.Eventing / Fluent fonts | Apache-2.0; [Uno](https://github.com/unoplatform/uno/blob/master/LICENSE.md), [Eventing](https://www.nuget.org/packages/Uno.Diagnostics.Eventing), [fonts](https://github.com/unoplatform/uno.fonts) |
| SkiaSharp and HarfBuzzSharp | MIT and native-component notices; [SkiaSharp source and notices](https://github.com/mono/SkiaSharp) |
| Uno.icu-win / ICU 77.1 | Apache-2.0 packaging / Unicode-3.0 and bundled component terms; [ICU notices](https://github.com/unicode-org/icu/blob/release-77-1/icu4c/LICENSE) |
| .NET and Windows interop tools | MIT and component notices; [.NET](https://github.com/dotnet/runtime), [CsWin32](https://github.com/microsoft/CsWin32) |
| xUnit / Microsoft.NET.Test.Sdk | Apache-2.0 / MIT; [xUnit](https://github.com/xunit/xunit), [VSTest](https://github.com/microsoft/vstest) |

Binary distributions must carry the licenses and notices for their actual runtime payload, along with GPL corresponding source. This source-only notice is not a complete binary-distribution notice bundle.
