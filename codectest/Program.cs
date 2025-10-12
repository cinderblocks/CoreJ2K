// Copyright (c) 2007-2016 CSJ2K contributors.
// Licensed under the BSD 3-Clause License.

using System.Runtime.InteropServices;
using CoreJ2K.Util;

namespace codectest
{
    using System;
    using System.IO;
    using SkiaSharp;

    using CoreJ2K;

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
                    SKBitmap image;
                    if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    {
                        var timer = new HiPerfTimer();
                        timer.Start();
                        image = J2kImage.FromFile(file).As<SKBitmap>();
                        timer.Stop();
                        Console.WriteLine($"{file}: {timer.Duration} seconds");
                    }
                    else
                    {
                        image = J2kImage.FromFile(file).As<SKBitmap>();
                    }

                    var histogram = GenerateHistogram(image);
                    var encoded = histogram.Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine("output", $"{Path.GetFileNameWithoutExtension (file)}_histogram.png"), encoded.ToArray());
                    encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                    File.WriteAllBytes(Path.Combine("output", $"{Path.GetFileNameWithoutExtension (file)}_encoded.png"), encoded.ToArray());
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
        }

        private static SKBitmap GenerateHistogram(SKBitmap image)
        {
            var histogram = new SKBitmap(256, 100, true);

            var colorcounts = new int[256];

            // This is ungodly slow, but it's just for diagnostics.
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

            var maxval = 0;
            for (var i = 0; i < 256; i++) if (colorcounts[i] > maxval) maxval = colorcounts[i];
            for (var i = 1; i < 255; i++)
            {
                //Console.WriteLine(i + ": " + histogram[i] + "," + (((float)histogram[i] / (float)maxval) * 100F));
                colorcounts[i] = (int)Math.Round((colorcounts[i] / (double)maxval) * 100D);
            }
            for (var x = 0; x < 256; x++)
            {
                for (var y = 0; y < 100; y++)
                {
                    histogram.SetPixel(x, y, colorcounts[x] >= (100 - y) ? SKColors.Black : SKColors.White);
                }
            }
            return histogram;
        }
    }
}
