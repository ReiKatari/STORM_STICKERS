using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.UI;

namespace STORM_STICKERS
{
    public enum StickerStatus
    {
        Pending,
        Processing,
        Success,
        Error
    }

    public class StickerItem : INotifyPropertyChanged
    {
        private string _outputName = "";
        private StickerStatus _status = StickerStatus.Pending;
        private string _statusMessage = "";
        private double _tolerance = 15.0; // default tolerance
        private bool _useFloodFill = true; // default contiguous
        private Color _targetColor;
        private SoftwareBitmapSource? _originalImageSource;
        private SoftwareBitmapSource? _previewImageSource;
        private bool _isProcessing = false;
        private bool _isCustomSettings = false;

        private readonly DispatcherQueue _dispatcherQueue;

        public string FilePath { get; }
        public string FileName { get; }
        public int Width { get; }
        public int Height { get; }
        public byte[] OriginalPixels { get; }
        public byte[] ProcessedPixels { get; private set; }
        public Color AutoBgColor { get; }

        public string OutputName
        {
            get => _outputName;
            set
            {
                if (_outputName != value)
                {
                    _outputName = value;
                    OnPropertyChanged();
                }
            }
        }

        public StickerStatus Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(StatusColor));
                    OnPropertyChanged(nameof(StatusText));
                }
            }
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set
            {
                if (_statusMessage != value)
                {
                    _statusMessage = value;
                    OnPropertyChanged();
                }
            }
        }

        public double Tolerance
        {
            get => _tolerance;
            set
            {
                if (Math.Abs(_tolerance - value) > 0.01)
                {
                    _tolerance = value;
                    OnPropertyChanged();
                    _ = UpdatePreviewAsync();
                }
            }
        }

        public bool UseFloodFill
        {
            get => _useFloodFill;
            set
            {
                if (_useFloodFill != value)
                {
                    _useFloodFill = value;
                    OnPropertyChanged();
                    _ = UpdatePreviewAsync();
                }
            }
        }

        public Color TargetColor
        {
            get => _targetColor;
            set
            {
                if (!_targetColor.Equals(value))
                {
                    _targetColor = value;
                    OnPropertyChanged();
                    _ = UpdatePreviewAsync();
                }
            }
        }

        public bool IsProcessing
        {
            get => _isProcessing;
            set
            {
                if (_isProcessing != value)
                {
                    _isProcessing = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsCustomSettings
        {
            get => _isCustomSettings;
            set
            {
                if (_isCustomSettings != value)
                {
                    _isCustomSettings = value;
                    OnPropertyChanged();
                }
            }
        }

        public SoftwareBitmapSource? OriginalImageSource
        {
            get => _originalImageSource;
            private set
            {
                _originalImageSource = value;
                OnPropertyChanged();
            }
        }

        public SoftwareBitmapSource? PreviewImageSource
        {
            get => _previewImageSource;
            private set
            {
                _previewImageSource = value;
                OnPropertyChanged();
            }
        }

        public string StatusText => Status switch
        {
            StickerStatus.Pending => "Ожидание",
            StickerStatus.Processing => "Обработка...",
            StickerStatus.Success => "Готово",
            StickerStatus.Error => "Ошибка",
            _ => "Неизвестно"
        };

        public string StatusColor => Status switch
        {
            StickerStatus.Pending => "#808080", // Gray
            StickerStatus.Processing => "#3498db", // Blue
            StickerStatus.Success => "#2ecc71", // Green
            StickerStatus.Error => "#e74c3c", // Red
            _ => "#808080"
        };

        public StickerItem(string filePath, string fileName, int width, int height, byte[] originalPixels, Color autoBgColor)
        {
            FilePath = filePath;
            FileName = fileName;
            Width = width;
            Height = height;
            OriginalPixels = originalPixels;
            ProcessedPixels = (byte[])originalPixels.Clone();
            AutoBgColor = autoBgColor;
            _targetColor = autoBgColor;
            
            // Default output filename: originalName_transparent.png
            string ext = Path.GetExtension(fileName);
            string baseName = Path.GetFileNameWithoutExtension(fileName);
            OutputName = $"{baseName}_transparent.png";

            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

            _ = InitializeThumbnailsAsync();
        }

        private async Task InitializeThumbnailsAsync()
        {
            // Create Original Image Source
            var originalBitmap = StickerProcessor.GetSoftwareBitmapFromPixels(OriginalPixels, Width, Height);
            var originalSource = new SoftwareBitmapSource();
            await originalSource.SetBitmapAsync(originalBitmap);
            OriginalImageSource = originalSource;

            // Generate first Preview
            await UpdatePreviewAsync();
        }

        public async Task UpdatePreviewAsync()
        {
            // Process pixels in a background thread
            byte[] processed = await Task.Run(() =>
                StickerProcessor.ProcessImage(OriginalPixels, Width, Height, TargetColor, Tolerance, UseFloodFill)
            );

            ProcessedPixels = processed;

            // Update UI element on UI thread
            _dispatcherQueue.TryEnqueue(async () =>
            {
                try
                {
                    var softwareBitmap = StickerProcessor.GetSoftwareBitmapFromPixels(ProcessedPixels, 512, 512);
                    
                    // SoftwareBitmapSource requires BGRA8 and Straight/Premultiplied alpha.
                    // SoftwareBitmap.Convert can be used if format is different, but ours is BGRA8 Straight already.
                    var previewSource = new SoftwareBitmapSource();
                    await previewSource.SetBitmapAsync(softwareBitmap);
                    PreviewImageSource = previewSource;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to update preview: {ex.Message}");
                }
            });
        }

        public async Task SaveAsync(string outputDir)
        {
            Status = StickerStatus.Processing;
            StatusMessage = "";
            try
            {
                string destPath = Path.Combine(outputDir, OutputName);
                await StickerProcessor.SaveImageAsync(ProcessedPixels, 512, 512, destPath);
                Status = StickerStatus.Success;
            }
            catch (Exception ex)
            {
                Status = StickerStatus.Error;
                StatusMessage = ex.Message;
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
