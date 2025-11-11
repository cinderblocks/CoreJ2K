// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

using System.Runtime.InteropServices;
using System.Diagnostics;
using CoreJ2K;
using SkiaSharp;
using System;
using System.IO;
using Pfim;
#if NET8_0_OR_GREATER
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
#endif

namespace codectest
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            if (Directory.Exists("output"))
            {
                var di = new DirectoryInfo("output");
                di.Delete(true);
            }
            Directory.CreateDirectory("output");

            using (var ppm = File.OpenRead(Path.Combine("samples", "a1_mono.ppm")))
            {
                var enc = J2kImage.ToBytes(J2kImage.CreateEncodableSource(ppm));
                File.WriteAllBytes(Path.Combine("output", "file11.jp2"), enc);
            }

            using (var ppm = File.OpenRead(Path.Combine("samples", "a2_colr.ppm")))
            {
                var enc = J2kImage.ToBytes(J2kImage.CreateEncodableSource(ppm));
                File.WriteAllBytes(Path.Combine("output", "file12.jp2"), enc);
            }

            using (var pgx = File.OpenRead(Path.Combine("samples", "c1p0_05_0.pgx")))
            {
                var enc = J2kImage.ToBytes(J2kImage.CreateEncodableSource(pgx));
                File.WriteAllBytes(Path.Combine("output", "file13.jp2"), enc);
            }

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "racoon.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file14.jp2"), enc);
            }

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn0g01.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file16.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn0g08.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file17.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn3p02.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file18.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn3p08.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file17.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn4a08.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file18.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn6a08.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file19.jp2"), enc);
            }
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "dog.jpeg")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "file20.jp2"), enc);
            }

            string[] files = Directory.GetFiles("output", "*.*", SearchOption.AllDirectories);
            foreach (var file in files)
            {
                try
                {
                    var sw = Stopwatch.StartNew();
                    var image = J2kImage.FromFile(file).As<SKBitmap>();
                    sw.Stop();
                    Console.WriteLine($"{file}: {sw.Elapsed.TotalSeconds} seconds");

                    var histogram = GenerateHistogram(image);
                    var encoded = histogram.Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine("output", $"{Path.GetFileNameWithoutExtension(file)}_histogram.png"), encoded.ToArray());
                    encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine("output", $"{Path.GetFileNameWithoutExtension(file)}_encoded.png"), encoded.ToArray());
                }
                catch (Exception e)
                {
                    Console.WriteLine($"{file}:\r\n{e.Message}");
                    if (e.InnerException != null)
                    {
                        Console.WriteLine(e.InnerException.Message);
                        Console.WriteLine(e.InnerException.StackTrace);
                    }
                    else Console.WriteLine(e.StackTrace);

                }
            }

            // ImageSharp-backed encoding use cases
#if NET8_0_OR_GREATER
            try
            {
                var samples = new[] { "racoon.png", "dog.jpeg" };
                foreach (var sample in samples)
                {
                    var path = Path.Combine("samples", sample);
                    if (!File.Exists(path)) continue;

                    using var img = Image.Load(path);
                    var enc = J2kImage.ToBytes(img);
                    var outpath = Path.Combine("output", Path.GetFileNameWithoutExtension(sample) + "_imagesharp.jp2");
                    File.WriteAllBytes(outpath, enc);

                    // Try decoding back to ImageSharp and save a PNG to verify roundtrip
                    try
                    {
                        var decoded = J2kImage.FromFile(outpath).As<Image>();
                        var decodedPath = Path.Combine("output", Path.GetFileNameWithoutExtension(outpath) + "_decoded.png");
                        decoded.SaveAsPng(decodedPath);
                        if (decoded is IDisposable d) d.Dispose();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"ImageSharp roundtrip for {sample} failed: {ex.Message}");
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"ImageSharp encoding failed: {e.Message}");
            }

            // Pfim usage examples - requires CoreJ2K.Pfim project referenced for net8.0
            try
            {
                // 1) Decode a JP2 to Pfim.IImage using the new decode-to-Pfim support
                var srcPath = Path.Combine("output", "file14.jp2");
                if (File.Exists(srcPath))
                {
                    var decoded = J2kImage.FromFile(srcPath).As<IImage>();
                    Console.WriteLine($"Decoded to Pfim IImage: {decoded.Width}x{decoded.Height}, format={decoded.Format}");

                    // Save raw bytes as a simple PNG via ImageSharp for verification if format is 8-bit RGB/RGBA
                    if (decoded.Format is ImageFormat.Rgb24 or ImageFormat.Rgba32)
                    {
                        var stride = decoded.Stride;
                        var bpp = decoded.BitsPerPixel;
                        using var img = Image.LoadPixelData<Rgba32>(decoded.Data, decoded.Width, decoded.Height);
                        img.SaveAsPng(Path.Combine("output", "file14_decoded_pfim.png"));
                    }

                    // 2) Use Pfim IImage as an encoding source (CoreJ2K.Pfim ImgReader supports Pfim input)
                    var encoded = J2kImage.ToBytes(decoded);
                    File.WriteAllBytes(Path.Combine("output", "file14_roundtrip_pfim.jp2"), encoded);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Pfim examples failed: {ex.Message}");
            }
#endif
        }

        private static SKBitmap GenerateHistogram(SKBitmap image)
        {
            const int width = 256;
            const int height = 100;

            var histogram = new SKBitmap(width, height, true);

            var colorcounts = new int[256];

            // Use SKPixmap raw buffer for high-throughput processing
            var pix = image.PeekPixels();
            if (pix != null)
            {
                var imgWidth = pix.Width;
                var imgHeight = pix.Height;
                var rowBytes = pix.RowBytes;
                var bytesPerPixel = pix.Info.BytesPerPixel;

                // Copy raw pixel buffer into managed array and operate on a Span<byte>
                var totalBytes = rowBytes * imgHeight;
                var raw = new byte[totalBytes];
                Marshal.Copy(pix.GetPixels(), raw, 0, totalBytes);
                var span = new Span<byte>(raw);

                var swizzle = pix.ColorType == SKColorType.Bgra8888
                              || pix.ColorType == SKColorType.Bgra1010102
                              || pix.ColorType == SKColorType.Bgr101010x;

                for (var y = 0; y < imgHeight; y++)
                {
                    var rowStart = y * rowBytes;
                    var baseIdx = rowStart;
                    for (var x = 0; x < imgWidth; x++)
                    {
                        var idx = baseIdx + x * bytesPerPixel;
                        byte r, g, b;
                        if (bytesPerPixel >= 3)
                        {
                            if (swizzle)
                            {
                                b = span[idx + 0];
                                g = span[idx + 1];
                                r = span[idx + 2];
                            }
                            else
                            {
                                r = span[idx + 0];
                                g = span[idx + 1];
                                b = span[idx + 2];
                            }
                        }
                        else if (bytesPerPixel == 2)
                        {
                            // Common packed formats (rgb565) are little-endian packed in two bytes.
                            // Fallback: expand to RGB approximately by bit masks.
                            var lo = span[idx + 0];
                            var hi = span[idx + 1];
                            var value = (ushort)(lo | (hi << 8));
                            // r: 5 bits, g:6 bits, b:5 bits
                            r = (byte)((value >> 11) & 0x1F);
                            g = (byte)((value >> 5) & 0x3F);
                            b = (byte)(value & 0x1F);
                            // Scale up to 0..255
                            r = (byte)((r * 255) / 31);
                            g = (byte)((g * 255) / 63);
                            b = (byte)((b * 255) / 31);
                        }
                        else
                        {
                            // Single byte (grayscale)
                            r = g = b = span[idx];
                        }

                        colorcounts[r]++;
                        colorcounts[g]++;
                        colorcounts[b]++;
                    }
                }
            }
            else
            {
                // Fallback: per-pixel access
                var pixels = image.Pixels;
                if (pixels != null && pixels.Length > 0)
                {
                    for (var i = 0; i < pixels.Length; i++)
                    {
                        var c = pixels[i];
                        colorcounts[c.Red]++;
                        colorcounts[c.Green]++;
                        colorcounts[c.Blue]++;
                    }
                }
                else
                {
                    for (var y = 0; y < image.Height; y++)
                    {
                        for (var x = 0; x < image.Width; x++)
                        {
                            var c = image.GetPixel(x, y);
                            colorcounts[c.Red]++;
                            colorcounts[c.Green]++;
                            colorcounts[c.Blue]++;
                        }
                    }
                }
            }

            var maxval = 0;
            for (var i = 0; i < 256; i++) if (colorcounts[i] > maxval) maxval = colorcounts[i];
            if (maxval == 0) maxval = 1; // prevent divide by zero

            // Normalize counts to 0..100
            var scaled = new byte[256];
            for (var i = 0; i < 256; i++)
            {
                // scale to 0..100
                scaled[i] = (byte)Math.Round((colorcounts[i] / (double)maxval) * 100D);
            }

            // Build pixel buffer for histogram: row-major, top-to-bottom
            var histPixels = new SKColor[width * height];

            for (var x = 0; x < width; x++)
            {
                var colHeight = scaled[x]; // 0..100
                // draw column: bottom pixels are black up to colHeight
                for (var y = 0; y < height; y++)
                {
                    // y = 0 is top row; we want bottom rows to be black for lower y index
                    var rowFromBottom = height - 1 - y;
                    var isBlack = rowFromBottom < colHeight;
                    histPixels[y * width + x] = isBlack ? SKColors.Black : SKColors.White;
                }
            }

            // Assign pixel buffer in one operation
            histogram.Pixels = histPixels;

            return histogram;
        }
    }
}
