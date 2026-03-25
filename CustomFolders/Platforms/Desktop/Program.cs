// SPDX-License-Identifier: GPL-3.0-or-later
using Uno.UI.Hosting;

namespace CustomFolders;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var host = UnoPlatformHostBuilder.Create()
            .App(() => new App())
            .UseWin32()
            .Build();
        host.Run();
    }
}
