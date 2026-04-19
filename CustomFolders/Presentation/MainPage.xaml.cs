// SPDX-License-Identifier: GPL-3.0-or-later
using System.Diagnostics;
using CustomFolders.Models;
using CustomFolders.Services;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using SkiaSharp;
using Windows.Storage.Pickers;

namespace CustomFolders.Presentation;

public sealed class VariantButtonItem
{
    public required string Label { get; init; }
    public required string VariantKey { get; init; }
    public Style? ButtonStyle { get; init; }
}

public sealed partial class MainPage : Page
{
    private readonly FolderIconConfig _config = new();
    private string? _selectedFolder;
    private string? _customImage;
    private bool _ready;
    private bool _applying;
    private const bool ApplyAvailable = true;

    public MainPage()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            _ready = true;
            RefreshVariantList();
            RefreshPreview();
        };
    }

    private void RefreshVariantList()
    {
        var presets = _config.IconType == IconType.Windows11 ? FolderPresetCatalog.Windows : FolderPresetCatalog.Mac;
        VariantList.ItemsSource = presets.Select(preset => new VariantButtonItem
        {
            Label = preset.Label,
            VariantKey = preset.Variant.ToString(),
            ButtonStyle = Resources[preset.Variant == _config.Variant ? "OptionBtnSelected" : "OptionBtn"] as Style,
        }).ToArray();
    }

    private void RefreshPreview()
    {
        if (!_ready) return;
        FolderPreview.Config = _config;
        FolderPreview.CustomIconPath = _customImage;
        FolderPreview.Invalidate();
    }

    private void SelectFamily(bool windows)
    {
        _config.Variant = windows ? FolderVariant.Windows11Default : FolderVariant.MacOsDefaultLight;
        _config.IconType = windows ? IconType.Windows11 : IconType.MacOs;
        BtnWindows.Style = (Style)Resources[windows ? "OptionBtnSelected" : "OptionBtn"];
        BtnMacOs.Style = (Style)Resources[windows ? "OptionBtn" : "OptionBtnSelected"];
        RefreshVariantList();
        RefreshPreview();
    }

    private void BtnWindows_Click(object sender, RoutedEventArgs e) => SelectFamily(true);
    private void BtnMacOs_Click(object sender, RoutedEventArgs e) => SelectFamily(false);

    private void VariantBtn_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || !Enum.TryParse<FolderVariant>(button.Tag?.ToString(), out var variant)) return;
        var preset = FolderPresetCatalog.Get(variant);
        _config.Variant = preset.Variant;
        _config.IconType = preset.Family;
        RefreshVariantList();
        RefreshPreview();
    }

    private static void InitializePicker(object picker)
    {
#if WINDOWS
        var window = App.MainWindow ?? throw new InvalidOperationException("The application window is not ready.");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, WinRT.Interop.WindowNative.GetWindowHandle(window));
#endif
    }

    private async void SelectFolderBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FolderPicker();
            picker.FileTypeFilter.Add("*");
            InitializePicker(picker);
            var folder = await picker.PickSingleFolderAsync();
            if (folder is null) return;
            _selectedFolder = folder.Path;
            SelectedFolderText.Text = _selectedFolder;
            SelectedFolderPanel.Visibility = Visibility.Visible;
            ApplyBtn.IsEnabled = ApplyAvailable;
        }
        catch (Exception ex) { ShowStatus($"Could not select a folder: {ex.Message}", false); }
    }

    private async void PickIconFile_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileOpenPicker();
            foreach (var extension in new[] { ".png", ".jpg", ".jpeg", ".bmp" }) picker.FileTypeFilter.Add(extension);
            InitializePicker(picker);
            var file = await picker.PickSingleFileAsync();
            if (file is null) return;
            // Validate the complete composition before changing the current selection.
            using var candidate = FolderIconService.RenderIcon(_config, file.Path);
            _customImage = file.Path;
            PickedIconLabel.Text = Path.GetFileName(file.Path);
            StatusPanel.Visibility = Visibility.Collapsed;
            RefreshPreview();
        }
        catch (Exception ex) { ShowStatus($"Could not open the image: {ex.Message}", false); }
    }

    private void SearchBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter) OpenImageSearch();
    }
    private void SearchBtn_Click(object sender, RoutedEventArgs e) => OpenImageSearch();
    private void OpenImageSearch()
    {
        try
        {
            string query = SearchBox.Text?.Trim() ?? "";
            var destination = query.Length == 0 ? "https://www.flaticon.com/"
                : "https://www.flaticon.com/search?word=" + Uri.EscapeDataString(query) + "&type=icon";
            Process.Start(new ProcessStartInfo(destination) { UseShellExecute = true });
        }
        catch (Exception ex) { ShowStatus($"Could not open the browser: {ex.Message}", false); }
    }

    private void TextLabelBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        _config.Text = TextLabelBox.Text;
        RefreshPreview();
    }

    private void EmojiIconBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!_ready) return;
        _config.EmojiIcon = EmojiIconBox.Text;
        // Typing an emoji switches away from the custom image layer.
        if (!string.IsNullOrWhiteSpace(_config.EmojiIcon))
        {
            _customImage = null;
            PickedIconLabel.Text = "Emoji selected";
        }
        RefreshPreview();
    }

    private void OnEmojiFlyoutOpened(object sender, object e)
    {
        if (EmojiPickerPivot.Items.Count != 0) return;
        foreach (var category in EmojiData.GetCategories())
        {
            var grid = new GridView
            {
                ItemsSource = category.Emojis,
                SelectionMode = ListViewSelectionMode.None,
                IsItemClickEnabled = true,
                ItemTemplate = (DataTemplate)Resources["EmojiTemplate"],
                Padding = new Thickness(8),
            };
            grid.ItemClick += EmojiGridView_ItemClick;
            EmojiPickerPivot.Items.Add(new PivotItem { Header = category.Icon, Content = grid });
        }
    }
    private void EmojiGridView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is string emoji) { EmojiIconBox.Text = emoji; EmojiFlyout.Hide(); }
    }
    private void AdjustColor_Changed(object sender, RoutedEventArgs e)
    {
        if (!_ready) return;
        _config.AdjustColor = AdjustColorCheck.IsChecked == true;
        RefreshPreview();
    }
    private void ScaleSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_ready) return;
        _config.ScaleFactor = e.NewValue;
        ScaleLabel.Text = $"{e.NewValue:P0}";
        RefreshPreview();
    }
    private void IconOffsetSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_ready) return;
        _config.IconOffsetY = e.NewValue;
        IconPosLabel.Text = $"{e.NewValue:0}px";
        RefreshPreview();
    }
    private void TextOffsetSlider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (!_ready) return;
        _config.TextOffsetY = e.NewValue;
        TextPosLabel.Text = $"{e.NewValue:0}px";
        RefreshPreview();
    }

    private async void ExportImageBtn_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var picker = new FileSavePicker { SuggestedFileName = "custom_folder_icon", SuggestedStartLocation = PickerLocationId.PicturesLibrary };
            picker.FileTypeChoices.Add("PNG image", new[] { ".png" });
            InitializePicker(picker);
            var file = await picker.PickSaveFileAsync();
            if (file is null) return;
            using var bitmap = FolderIconService.RenderIcon(_config, _customImage);
            using var image = SKImage.FromBitmap(bitmap);
            using var data = image.Encode(SKEncodedImageFormat.Png, 100);
            using var output = await file.OpenStreamForWriteAsync();
            output.SetLength(0);
            data.SaveTo(output);
            ShowStatus("PNG exported.", true);
        }
        catch (Exception ex) { ShowStatus($"Export failed: {ex.Message}", false); }
    }

    private async void ApplyBtn_Click(object sender, RoutedEventArgs e)
    {
        if (_applying || string.IsNullOrWhiteSpace(_selectedFolder)) return;
        _applying = true;
        ApplyBtn.IsEnabled = false;
        ApplyBtn.Content = "Applying…";
        try
        {
            using var bitmap = FolderIconService.RenderIcon(_config, _customImage);
            var error = await FolderIconService.ApplyIconToFolderAsync(_selectedFolder, bitmap);
            ShowStatus(error ?? "Icon applied. Explorer may take a moment to refresh.", error is null);
        }
        catch (Exception ex) { ShowStatus($"Could not apply the icon: {ex.Message}", false); }
        finally
        {
            _applying = false;
            ApplyBtn.Content = "Apply to Folder";
            ApplyBtn.IsEnabled = ApplyAvailable && _selectedFolder is not null;
        }
    }

    private void ShowStatus(string message, bool success)
    {
        StatusPanel.Background = new SolidColorBrush(success ? Windows.UI.Color.FromArgb(255, 28, 64, 54) : Windows.UI.Color.FromArgb(255, 77, 38, 42));
        StatusPanel.BorderBrush = new SolidColorBrush(success ? Windows.UI.Color.FromArgb(255, 91, 184, 141) : Windows.UI.Color.FromArgb(255, 222, 124, 133));
        StatusText.Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 240, 245, 249));
        StatusText.Text = message;
        StatusPanel.Visibility = Visibility.Visible;
    }
}
