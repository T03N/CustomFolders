// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.UI.Xaml;
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
    }
}
