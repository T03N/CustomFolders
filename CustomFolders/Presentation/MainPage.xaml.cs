// SPDX-License-Identifier: GPL-3.0-or-later
using Microsoft.UI.Xaml.Controls;
using CustomFolders.Models;
namespace CustomFolders.Presentation;
public sealed partial class MainPage : Page { public MainPage() { InitializeComponent(); FolderPreview.Config = new FolderIconConfig(); } }
