using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;

namespace STORM_STICKERS
{
    public sealed partial class MainPage : Page, INotifyPropertyChanged
    {
        private SoftwareBitmapSource? _checkerboardSource;
        private bool _isUpdatingDetails = false;
        private bool _isGlobalSettingUpdating = false;

        public ObservableCollection<StickerItem> StickerItems { get; } = new();

        public SoftwareBitmapSource? CheckerboardSource
        {
            get => _checkerboardSource;
            set
            {
                if (_checkerboardSource != value)
                {
                    _checkerboardSource = value;
                    OnPropertyChanged();
                }
            }
        }

        public MainPage()
        {
            InitializeComponent();
            DataContext = this;
            StickerListView.ItemsSource = StickerItems;
            InitializeCheckerboardSource();
            UpdateUIState();
        }

        private async void InitializeCheckerboardSource()
        {
            int width = 32;
            int height = 32;
            int size = 4;
            byte[] pixels = new byte[width * height * 4];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool isLight = ((x / size) + (y / size)) % 2 == 0;
                    byte colorVal = (byte)(isLight ? 255 : 230);
                    int idx = (y * width + x) * 4;
                    pixels[idx] = colorVal;     // B
                    pixels[idx + 1] = colorVal; // G
                    pixels[idx + 2] = colorVal; // R
                    pixels[idx + 3] = 255;      // A
                }
            }

            SoftwareBitmap softwareBitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
            softwareBitmap.CopyFromBuffer(pixels.AsBuffer());

            var source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);
            CheckerboardSource = source;
        }

        private void UpdateUIState()
        {
            bool hasItems = StickerItems.Count > 0;
            GridDropZone.Visibility = hasItems ? Visibility.Collapsed : Visibility.Visible;
            GridQueueContent.Visibility = hasItems ? Visibility.Visible : Visibility.Collapsed;
            BtnClearQueue.IsEnabled = hasItems;
        }

        private bool ContainsFile(string path)
        {
            return StickerItems.Any(item => string.Equals(item.FilePath, path, StringComparison.OrdinalIgnoreCase));
        }

        private async Task AddFilesToQueueAsync(IEnumerable<StorageFile> files)
        {
            foreach (var file in files)
            {
                if (ContainsFile(file.Path)) continue;

                // Validate extension
                string ext = Path.GetExtension(file.Name).ToLower();
                if (ext != ".png" && ext != ".jpg" && ext != ".jpeg" && ext != ".webp")
                    continue;

                try
                {
                    var loaded = await StickerProcessor.LoadImageAsync(file);
                    var item = new StickerItem(
                        file.Path,
                        file.Name,
                        loaded.Width,
                        loaded.Height,
                        loaded.PixelData,
                        loaded.AutoBgColor
                    );

                    // Auto set output folder to the folder of the first file if empty
                    if (string.IsNullOrEmpty(TxtOutputPath.Text))
                    {
                        TxtOutputPath.Text = Path.GetDirectoryName(file.Path) ?? "";
                    }

                    SyncItemToGlobal(item);
                    StickerItems.Add(item);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load image: {ex.Message}");
                }
            }

            UpdateUIState();

            // Select the newly added item if none is selected
            if (StickerListView.SelectedItem == null && StickerItems.Count > 0)
            {
                StickerListView.SelectedIndex = StickerItems.Count - 1;
            }
        }

        private void SyncItemToGlobal(StickerItem item)
        {
            if (item.IsCustomSettings) return;

            item.UseFloodFill = RadioFloodFill.IsChecked == true;
            item.Tolerance = SliderGlobalTolerance.Value;
            
            if (ChkAutoDetectColor.IsChecked == true)
            {
                item.TargetColor = item.AutoBgColor;
            }
            else
            {
                item.TargetColor = GlobalColorPicker.Color;
            }
        }

        private void SyncAllToGlobal()
        {
            if (_isGlobalSettingUpdating) return;
            if (GlobalColorPicker == null || ChkAutoDetectColor == null || SliderGlobalTolerance == null || RectGlobalColorPreview == null || RadioFloodFill == null) return;
            _isGlobalSettingUpdating = true;

            Color targetColor = GlobalColorPicker.Color;
            bool useFloodFill = RadioFloodFill.IsChecked == true;
            double tolerance = SliderGlobalTolerance.Value;
            bool autoColor = ChkAutoDetectColor.IsChecked == true;

            if (autoColor)
            {
                // Show transparent or default indicator
                RectGlobalColorPreview.Background = new SolidColorBrush(Color.FromArgb(50, 128, 128, 128));
            }
            else
            {
                RectGlobalColorPreview.Background = new SolidColorBrush(targetColor);
            }

            foreach (var item in StickerItems)
            {
                if (!item.IsCustomSettings)
                {
                    item.UseFloodFill = useFloodFill;
                    item.Tolerance = tolerance;
                    item.TargetColor = autoColor ? item.AutoBgColor : targetColor;
                }
            }

            _isGlobalSettingUpdating = false;
        }

        // --- DRAG AND DROP FILE IMPORT ---

        private void GridDropZone_DragOver(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Добавить стикеры";
            e.DragUIOverride.IsCaptionVisible = true;
            e.DragUIOverride.IsContentVisible = true;
        }

        private void GridDropZone_DragLeave(object sender, DragEventArgs e)
        {
        }

        private async void GridDropZone_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                List<StorageFile> files = new();

                foreach (var item in items)
                {
                    if (item is StorageFile file)
                    {
                        files.Add(file);
                    }
                    else if (item is StorageFolder folder)
                    {
                        // Add all images in the folder
                        var folderFiles = await folder.GetFilesAsync();
                        files.AddRange(folderFiles);
                    }
                }

                await AddFilesToQueueAsync(files);
            }
        }

        // --- DRAG AND DROP FOR SAVE PATH ---

        private void TxtOutputPath_DragOver(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                e.AcceptedOperation = DataPackageOperation.Copy;
                e.DragUIOverride.Caption = "Указать как папку сохранения";
                e.DragUIOverride.IsCaptionVisible = true;
            }
        }

        private async void TxtOutputPath_Drop(object sender, DragEventArgs e)
        {
            if (e.DataView.Contains(StandardDataFormats.StorageItems))
            {
                var items = await e.DataView.GetStorageItemsAsync();
                if (items.Count > 0)
                {
                    var item = items[0];
                    if (item is StorageFolder folder)
                    {
                        TxtOutputPath.Text = folder.Path;
                    }
                    else if (item is StorageFile file)
                    {
                        TxtOutputPath.Text = Path.GetDirectoryName(file.Path) ?? "";
                    }
                }
            }
        }

        // --- ACTIONS ---

        private async void BtnBrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FolderPicker();
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add("*");

            if (MainWindow.Instance != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Instance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            StorageFolder folder = await picker.PickSingleFolderAsync();
            if (folder != null)
            {
                TxtOutputPath.Text = folder.Path;
            }
        }

        private async void BtnAddFiles_Click(object sender, RoutedEventArgs e)
        {
            var picker = new Windows.Storage.Pickers.FileOpenPicker();
            picker.ViewMode = Windows.Storage.Pickers.PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary;
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".webp");

            if (MainWindow.Instance != null)
            {
                var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(MainWindow.Instance);
                WinRT.Interop.InitializeWithWindow.Initialize(picker, hwnd);
            }

            var files = await picker.PickMultipleFilesAsync();
            if (files != null && files.Count > 0)
            {
                await AddFilesToQueueAsync(files);
            }
        }

        private void BtnClearQueue_Click(object sender, RoutedEventArgs e)
        {
            StickerItems.Clear();
            UpdateUIState();
            StickerListView_SelectionChanged(this, null!);
        }

        private void BtnRemoveItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.DataContext is StickerItem item)
            {
                StickerItems.Remove(item);
                UpdateUIState();
                if (StickerListView.SelectedItem == null && StickerItems.Count > 0)
                {
                    StickerListView.SelectedIndex = 0;
                }
            }
        }

        private async void BtnProcessAll_Click(object sender, RoutedEventArgs e)
        {
            string outputDir = TxtOutputPath.Text.Trim();
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
            {
                ContentDialog dialog = new ContentDialog
                {
                    Title = "Ошибка",
                    Content = "Пожалуйста, укажите существующую папку для сохранения стикеров.",
                    CloseButtonText = "OK",
                    XamlRoot = this.XamlRoot
                };
                await dialog.ShowAsync();
                return;
            }

            BtnProcessAll.IsEnabled = false;

            // Save all items
            var tasks = StickerItems.Select(item => item.SaveAsync(outputDir)).ToList();
            await Task.WhenAll(tasks);

            BtnProcessAll.IsEnabled = true;

            // Notify user of completion
            int successCount = StickerItems.Count(item => item.Status == StickerStatus.Success);
            int errorCount = StickerItems.Count(item => item.Status == StickerStatus.Error);

            ContentDialog resultDialog = new ContentDialog
            {
                Title = "Сохранение завершено",
                Content = $"Успешно обработано и сохранено: {successCount}\nОшибок: {errorCount}\n\nФайлы сохранены в папку:\n{outputDir}\n\nОбратите внимание: оригинальные файлы НЕ перезаписываются. Итоговые прозрачные стикеры сохранены рядом с суффиксом '_transparent.png'.",
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            };
            await resultDialog.ShowAsync();

            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = outputDir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch { }
        }

        // --- GLOBAL SETTINGS EVENTS ---

        private void GlobalSetting_Changed(object sender, RoutedEventArgs e)
        {
            SyncAllToGlobal();
        }

        private void ChkAutoDetectColor_Changed(object sender, RoutedEventArgs e)
        {
            if (BtnPickGlobalColor != null)
            {
                BtnPickGlobalColor.IsEnabled = ChkAutoDetectColor.IsChecked != true;
            }
            SyncAllToGlobal();
        }

        private void GlobalColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            SyncAllToGlobal();
        }

        private void SliderGlobalTolerance_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            SyncAllToGlobal();
        }

        // --- DETAILED PANEL SELECTION & EVENTS ---

        private void StickerListView_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected == null)
            {
                DetailSettingsPanel.IsEnabled = false;
                PanelLocalControls.IsEnabled = false;
                TxtInfoFileName.Text = "Имя: -";
                TxtInfoResolution.Text = "Разрешение: -";
                TxtInfoPath.Text = "Путь: -";
                TxtInfoStatusMsg.Text = "";
                return;
            }

            DetailSettingsPanel.IsEnabled = true;

            _isUpdatingDetails = true;

            ChkCustomSettings.IsChecked = selected.IsCustomSettings;
            PanelLocalControls.IsEnabled = selected.IsCustomSettings;

            if (selected.UseFloodFill)
                RadioLocalFloodFill.IsChecked = true;
            else
                RadioLocalGlobal.IsChecked = true;

            LocalColorPicker.Color = selected.TargetColor;
            RectLocalColorPreview.Background = new SolidColorBrush(selected.TargetColor);
            SliderLocalTolerance.Value = selected.Tolerance;

            TxtInfoFileName.Text = $"Имя: {selected.FileName}";
            TxtInfoResolution.Text = $"Разрешение: {selected.Width} x {selected.Height}";
            TxtInfoPath.Text = $"Путь: {selected.FilePath}";
            TxtInfoStatusMsg.Text = selected.StatusMessage;

            _isUpdatingDetails = false;
        }

        private void ChkCustomSettings_Checked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingDetails) return;
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected != null)
            {
                selected.IsCustomSettings = true;
                PanelLocalControls.IsEnabled = true;

                // Sync controls to current values
                RadioLocalFloodFill.IsChecked = selected.UseFloodFill;
                RadioLocalGlobal.IsChecked = !selected.UseFloodFill;
                LocalColorPicker.Color = selected.TargetColor;
                RectLocalColorPreview.Background = new SolidColorBrush(selected.TargetColor);
                SliderLocalTolerance.Value = selected.Tolerance;
            }
        }

        private void ChkCustomSettings_Unchecked(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingDetails) return;
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected != null)
            {
                selected.IsCustomSettings = false;
                PanelLocalControls.IsEnabled = false;

                // Reset to global settings
                SyncItemToGlobal(selected);
            }
        }

        private void LocalSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (_isUpdatingDetails) return;
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected != null && selected.IsCustomSettings)
            {
                selected.UseFloodFill = RadioLocalFloodFill.IsChecked == true;
            }
        }

        private void LocalColorPicker_ColorChanged(ColorPicker sender, ColorChangedEventArgs args)
        {
            if (_isUpdatingDetails) return;
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected != null && selected.IsCustomSettings)
            {
                selected.TargetColor = LocalColorPicker.Color;
                RectLocalColorPreview.Background = new SolidColorBrush(LocalColorPicker.Color);
            }
        }

        private void SliderLocalTolerance_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            if (_isUpdatingDetails) return;
            var selected = StickerListView.SelectedItem as StickerItem;
            if (selected != null && selected.IsCustomSettings)
            {
                selected.Tolerance = SliderLocalTolerance.Value;
            }
        }

        // --- PROPERTY CHANGED ---

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
