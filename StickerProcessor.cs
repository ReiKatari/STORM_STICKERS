using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace STORM_STICKERS
{
    public class LoadedSticker
    {
        public byte[] PixelData { get; set; } = Array.Empty<byte>();
        public int Width { get; set; }
        public int Height { get; set; }
        public Color AutoBgColor { get; set; }
    }

    public static class StickerProcessor
    {
        public static async Task<LoadedSticker> LoadImageAsync(StorageFile file)
        {
            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(stream);
                BitmapFrame frame = await decoder.GetFrameAsync(0);
                
                PixelDataProvider pixelProvider = await frame.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.ColorManageToSRgb
                );

                byte[] pixels = pixelProvider.DetachPixelData();
                int width = (int)frame.OrientedPixelWidth;
                int height = (int)frame.OrientedPixelHeight;

                Color autoColor = DetectBackgroundColor(pixels, width, height);

                return new LoadedSticker
                {
                    PixelData = pixels,
                    Width = width,
                    Height = height,
                    AutoBgColor = autoColor
                };
            }
        }

        private static Color DetectBackgroundColor(byte[] pixels, int width, int height)
        {
            // Sample the 4 corners:
            // Top-left: (0, 0)
            // Top-right: (width - 1, 0)
            // Bottom-left: (0, height - 1)
            // Bottom-right: (width - 1, height - 1)
            int bpp = 4;
            int tlIdx = 0;
            int trIdx = (width - 1) * bpp;
            int blIdx = (height - 1) * width * bpp;
            int brIdx = ((height - 1) * width + (width - 1)) * bpp;

            Color GetColorAt(int idx)
            {
                if (idx < 0 || idx + 3 >= pixels.Length)
                    return Color.FromArgb(255, 0, 0, 0);
                return Color.FromArgb(255, pixels[idx + 2], pixels[idx + 1], pixels[idx]);
            }

            Color tl = GetColorAt(tlIdx);
            Color tr = GetColorAt(trIdx);
            Color bl = GetColorAt(blIdx);
            Color br = GetColorAt(brIdx);

            // Since backgrounds are usually solid, let's find the most common color
            // or return the top-left color as a default.
            // Let's count matching corners with a small tolerance.
            Color[] corners = { tl, tr, bl, br };
            int maxMatches = 0;
            Color bestColor = tl;

            for (int i = 0; i < 4; i++)
            {
                int matches = 0;
                for (int j = 0; j < 4; j++)
                {
                    if (IsSimilar(corners[i], corners[j], 10.0))
                    {
                        matches++;
                    }
                }
                if (matches > maxMatches)
                {
                    maxMatches = matches;
                    bestColor = corners[i];
                }
            }

            return bestColor;
        }

        private static bool IsSimilar(Color c1, Color c2, double tolerance)
        {
            double db = c1.B - c2.B;
            double dg = c1.G - c2.G;
            double dr = c1.R - c2.R;
            double distSq = db * db + dg * dg + dr * dr;
            double maxDist = (tolerance / 100.0) * 441.67;
            return distSq <= maxDist * maxDist;
        }

        public static byte[] ProcessImage(byte[] originalPixels, int width, int height, Color targetColor, double tolerancePercent, bool floodFill)
        {
            int bytesPerPixel = 4;
            byte[] result = (byte[])originalPixels.Clone();
            bool[] visited = new bool[width * height];

            byte targetB = targetColor.B;
            byte targetG = targetColor.G;
            byte targetR = targetColor.R;

            // Tolerance from 0% to 100%. Max Euclidean distance is ~441.67.
            double maxDist = (tolerancePercent / 100.0) * 441.67;
            double maxDistSq = maxDist * maxDist;

            bool ColorMatch(int idx)
            {
                byte b = result[idx];
                byte g = result[idx + 1];
                byte r = result[idx + 2];
                byte a = result[idx + 3];
                if (a == 0) return false; // already transparent

                double db = b - targetB;
                double dg = g - targetG;
                double dr = r - targetR;
                double distSq = db * db + dg * dg + dr * dr;
                return distSq <= maxDistSq;
            }

            if (floodFill)
            {
                Queue<int> queue = new Queue<int>();

                // Add all boundary pixels to the queue if they match the background color
                // Top and bottom boundaries
                for (int x = 0; x < width; x++)
                {
                    AddPixel(x, 0);
                    AddPixel(x, height - 1);
                }
                // Left and right boundaries (excluding corners which were already added)
                for (int y = 1; y < height - 1; y++)
                {
                    AddPixel(0, y);
                    AddPixel(width - 1, y);
                }

                void AddPixel(int x, int y)
                {
                    int idx = y * width + x;
                    if (!visited[idx] && ColorMatch(idx * bytesPerPixel))
                    {
                        visited[idx] = true;
                        queue.Enqueue(idx);
                    }
                }

                int[] dx = { 0, 0, 1, -1 };
                int[] dy = { 1, -1, 0, 0 };

                while (queue.Count > 0)
                {
                    int curr = queue.Dequeue();
                    int currX = curr % width;
                    int currY = curr / width;

                    // Make transparent
                    int pixelIdx = curr * bytesPerPixel;
                    result[pixelIdx + 3] = 0; // Alpha = 0

                    // Check 4-way neighbors
                    for (int i = 0; i < 4; i++)
                    {
                        int nx = currX + dx[i];
                        int ny = currY + dy[i];

                        if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                        {
                            int nIdx = ny * width + nx;
                            if (!visited[nIdx] && ColorMatch(nIdx * bytesPerPixel))
                            {
                                visited[nIdx] = true;
                                queue.Enqueue(nIdx);
                            }
                        }
                    }
                }
            }
            else
            {
                // Global color replacement
                for (int i = 0; i < result.Length; i += bytesPerPixel)
                {
                    if (ColorMatch(i))
                    {
                        result[i + 3] = 0; // Alpha = 0
                    }
                }
            }

            return ResizeTo512(result, width, height);
        }

        private static byte[] ResizeTo512(byte[] sourcePixels, int width, int height)
        {
            double scale = Math.Min(512.0 / width, 512.0 / height);
            int sw = (int)Math.Max(1, Math.Round(width * scale));
            int sh = (int)Math.Max(1, Math.Round(height * scale));

            byte[] resized = new byte[sw * sh * 4];
            float xRatio = (float)(width - 1) / sw;
            float yRatio = (float)(height - 1) / sh;

            for (int y = 0; y < sh; y++)
            {
                for (int x = 0; x < sw; x++)
                {
                    int xL = (int)Math.Floor(xRatio * x);
                    int xH = (int)Math.Ceiling(xRatio * x);
                    int yL = (int)Math.Floor(yRatio * y);
                    int yH = (int)Math.Ceiling(yRatio * y);

                    float xWeight = (xRatio * x) - xL;
                    float yWeight = (yRatio * y) - yL;

                    int idx00 = (yL * width + xL) * 4;
                    int idx10 = (yL * width + xH) * 4;
                    int idx01 = (yH * width + xL) * 4;
                    int idx11 = (yH * width + xH) * 4;

                    int destIdx = (y * sw + x) * 4;

                    for (int c = 0; c < 4; c++)
                    {
                        float val00 = sourcePixels[idx00 + c];
                        float val10 = sourcePixels[idx10 + c];
                        float val01 = sourcePixels[idx01 + c];
                        float val11 = sourcePixels[idx11 + c];

                        float val = val00 * (1 - xWeight) * (1 - yWeight) +
                                    val10 * xWeight * (1 - yWeight) +
                                    val01 * (1 - xWeight) * yWeight +
                                    val11 * xWeight * yWeight;

                        resized[destIdx + c] = (byte)Math.Clamp(val, 0, 255);
                    }
                }
            }

            byte[] finalPixels = new byte[512 * 512 * 4];
            int startX = (512 - sw) / 2;
            int startY = (512 - sh) / 2;

            for (int y = 0; y < sh; y++)
            {
                int srcOffset = y * sw * 4;
                int destOffset = ((startY + y) * 512 + startX) * 4;
                System.Buffer.BlockCopy(resized, srcOffset, finalPixels, destOffset, sw * 4);
            }

            return finalPixels;
        }

        public static async Task SaveImageAsync(byte[] pixels, int width, int height, string destinationPath)
        {
            string folderPath = Path.GetDirectoryName(destinationPath) ?? "";
            StorageFolder folder = await StorageFolder.GetFolderFromPathAsync(folderPath);
            string fileName = Path.GetFileName(destinationPath);
            StorageFile file = await folder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);

            using (IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.ReadWrite))
            {
                BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
                encoder.SetPixelData(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Straight,
                    (uint)width,
                    (uint)height,
                    96.0,
                    96.0,
                    pixels
                );
                await encoder.FlushAsync();
            }
        }

        public static SoftwareBitmap GetSoftwareBitmapFromPixels(byte[] pixels, int width, int height)
        {
            SoftwareBitmap bitmap = new SoftwareBitmap(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Straight);
            bitmap.CopyFromBuffer(pixels.AsBuffer());
            return SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
        }
    }
}
