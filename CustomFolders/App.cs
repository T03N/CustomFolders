// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
namespace CustomFolders;
public sealed class App : Application
{
    private Window? _window;
    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new Window { Title = "CustomFolders", Content = new TextBlock { Text = "CustomFolders", FontSize = 32 } };
        _window.Activate();
    }
}
