using System;
using System.IO;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace STORM_STICKERS
{
    public static class IconGenerator
    {
        public static async Task GenerateAppIconAsync()
        {
            string projectRoot = @"E:\STORM STICKERS";
            string jpgPath = Path.Combine(projectRoot, "app_icon.jpg");
            string icoPath = Path.Combine(projectRoot, "Assets", "AppIcon.ico");

            if (!File.Exists(jpgPath))
            {
                return;
            }

            try
            {
                var file = await StorageFile.GetFileFromPathAsync(jpgPath);
                using (var stream = await file.OpenAsync(FileAccessMode.Read))
                {
                    var decoder = await BitmapDecoder.CreateAsync(stream);
                    var frame = await decoder.GetFrameAsync(0);
                    
                    var pixelProvider = await frame.GetPixelDataAsync(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        new BitmapTransform(),
                        ExifOrientationMode.IgnoreExifOrientation,
                        ColorManagementMode.ColorManageToSRgb
                    );
                    byte[] pixels = pixelProvider.DetachPixelData();

                    // Re-encode to PNG in memory
                    var ms = new InMemoryRandomAccessStream();
                    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, ms);
                    encoder.SetPixelData(
                        BitmapPixelFormat.Bgra8,
                        BitmapAlphaMode.Straight,
                        frame.OrientedPixelWidth,
                        frame.OrientedPixelHeight,
                        96.0,
                        96.0,
                        pixels
                    );
                    await encoder.FlushAsync();

                    // Read PNG bytes
                    byte[] pngBytes = new byte[ms.Size];
                    using (var reader = new DataReader(ms.GetInputStreamAt(0)))
                    {
                        await reader.LoadAsync((uint)ms.Size);
                        reader.ReadBytes(pngBytes);
                    }

                    // Write standard ICO file container with PNG inside
                    using (var fs = File.Open(icoPath, FileMode.Create, FileAccess.Write))
                    {
                        fs.WriteByte(0); fs.WriteByte(0); // Reserved
                        fs.WriteByte(1); fs.WriteByte(0); // Type: Icon (1)
                        fs.WriteByte(1); fs.WriteByte(0); // Count: 1 image

                        // Entry
                        fs.WriteByte(0); // Width: 256 (0 represents 256)
                        fs.WriteByte(0); // Height: 256 (0 represents 256)
                        fs.WriteByte(0); // Color count: 0 (no palette)
                        fs.WriteByte(0); // Reserved
                        fs.WriteByte(1); fs.WriteByte(0); // Color planes: 1
                        fs.WriteByte(32); fs.WriteByte(0); // Bits per pixel: 32

                        // Data size
                        int size = pngBytes.Length;
                        fs.WriteByte((byte)(size & 0xFF));
                        fs.WriteByte((byte)((size >> 8) & 0xFF));
                        fs.WriteByte((byte)((size >> 16) & 0xFF));
                        fs.WriteByte((byte)((size >> 24) & 0xFF));

                        // Offset (Header 6 + Entry 16 = 22)
                        int offset = 22;
                        fs.WriteByte((byte)(offset & 0xFF));
                        fs.WriteByte((byte)((offset >> 8) & 0xFF));
                        fs.WriteByte((byte)((offset >> 16) & 0xFF));
                        fs.WriteByte((byte)((offset >> 24) & 0xFF));

                        // PNG data bytes
                        fs.Write(pngBytes, 0, pngBytes.Length);
                    }
                }

                // Also copy it to the build output directory if the app is already compiled
                string binIcoPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
                string binDir = Path.GetDirectoryName(binIcoPath) ?? "";
                if (Directory.Exists(binDir))
                {
                    File.Copy(icoPath, binIcoPath, true);
                }
            }
            catch (Exception ex)
            {
                File.WriteAllText(Path.Combine(projectRoot, "icon_generation_error.txt"), ex.ToString());
            }
        }
    }
}
