// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.UI.Xaml;
using Uno.Resizetizer;
using CustomFolders.Presentation;

namespace CustomFolders;

public sealed partial class App : Application
{
    public static Window? MainWindow { get; private set; }

    public App() => InitializeComponent();

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        MainWindow = new Window { Title = "CustomFolders", Content = new Shell() };
        MainWindow.Activate();
        MainWindow.AppWindow.Resize(new Windows.Graphics.SizeInt32 { Width = 1320, Height = 920 });
        MainWindow.SetWindowIcon();
        MainWindow.Closed += (_, _) => Services.FolderIconService.Dispose();
    }
}
