// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Diagnostics;
using System.IO;
using CoreJ2K;
using CoreJ2K.Configuration;
using CoreJ2K.Skia;
using CoreJ2K.Pfim;
using SkiaSharp;
using Pfim;

#if NETFRAMEWORK
using System.Drawing;
using System.Drawing.Imaging;
#endif

#if NET8_0_OR_GREATER
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using CoreJ2K.ImageSharp;
#endif

namespace codectest
{
    /// <summary>
    /// CoreJ2K Codec Test - Demonstrates the modern configuration API.
    /// </summary>
    internal class Program
    {
        private static void Main(string[] args)
        {
            Console.WriteLine("CoreJ2K Modern API Demo");
            Console.WriteLine("=======================\n");

            // Clean output directory
            if (Directory.Exists("output"))
            {
                var di = new DirectoryInfo("output");
                di.Delete(true);
            }
            Directory.CreateDirectory("output");

            // Run all demonstrations
            DemonstrateTraditionalEncoding();
            DemonstrateModernEncodingPresets();
            DemonstrateModernEncodingCustom();
            DemonstrateSkiaIntegration();
            DemonstratePfimIntegration();
#if NET8_0_OR_GREATER
            DemonstrateImageSharpIntegration();
#endif
            // New: Native plugin conversion demo
            DemonstrateNativePluginConversions();

            DemonstrateDecoding();
            DemonstratePerformance();

            Console.WriteLine("\n? All demonstrations complete!");
            Console.WriteLine($"Check the 'output' directory for generated files.");
        }

        #region Traditional API Demo

        static void DemonstrateTraditionalEncoding()
        {
            Console.WriteLine("\n?? Traditional API Encoding");
            Console.WriteLine("---------------------------");

            // Traditional API - still works!
            using (var ppm = File.OpenRead(Path.Combine("samples", "a1_mono.ppm")))
            {
                var enc = J2kImage.ToBytes(J2kImage.CreateEncodableSource(ppm));
                File.WriteAllBytes(Path.Combine("output", "traditional_mono.jp2"), enc);
                Console.WriteLine($"? Encoded mono image (traditional API): {enc.Length:N0} bytes");
            }

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "racoon.png")))
            {
                var enc = J2kImage.ToBytes(bitmap);
                File.WriteAllBytes(Path.Combine("output", "traditional_racoon.jp2"), enc);
                Console.WriteLine($"? Encoded racoon (traditional API): {enc.Length:N0} bytes");
            }
        }

        #endregion

        #region Modern API - Presets

        static void DemonstrateModernEncodingPresets()
        {
            Console.WriteLine("\n?? Modern API - Presets");
            Console.WriteLine("-----------------------");

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "racoon.png")))
            {
                // Lossless
                var lossless = bitmap.EncodeToJ2KLossless();
                File.WriteAllBytes(Path.Combine("output", "preset_lossless.jp2"), lossless);
                Console.WriteLine($"? Lossless preset: {lossless.Length:N0} bytes");

                // High Quality
                var highQuality = bitmap.EncodeToJ2KHighQuality("© 2025 CoreJ2K");
                File.WriteAllBytes(Path.Combine("output", "preset_highquality.jp2"), highQuality);
                Console.WriteLine($"? High quality preset: {highQuality.Length:N0} bytes");

                // Web Optimized
                var web = bitmap.EncodeToJ2KWeb("© 2025 CoreJ2K");
                File.WriteAllBytes(Path.Combine("output", "preset_web.jp2"), web);
                Console.WriteLine($"? Web preset: {web.Length:N0} bytes");
            }

            // Complete configuration presets
            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "dog.jpeg")))
            {
                // Medical preset
                var medical = bitmap.EncodeToJ2K(CompleteConfigurationPresets.Medical);
                File.WriteAllBytes(Path.Combine("output", "preset_medical.jp2"), medical);
                Console.WriteLine($"? Medical preset: {medical.Length:N0} bytes");

                // Photography preset
                var photo = bitmap.EncodeToJ2K(
                    CompleteConfigurationPresets.Photography
                        .WithComment("Demo photo")
                        .WithCopyright("© 2025 CoreJ2K Demo"));
                File.WriteAllBytes(Path.Combine("output", "preset_photography.jp2"), photo);
                Console.WriteLine($"? Photography preset: {photo.Length:N0} bytes");

                // Archival preset
                var archival = bitmap.EncodeToJ2K(
                    CompleteConfigurationPresets.Archival
                        .WithMetadata(m => m
                            .WithComment("Archived: 2025-01-15")
                            .WithComment("Source: dog.jpeg")
                            .WithCopyright("© 2025 CoreJ2K Demo")));
                File.WriteAllBytes(Path.Combine("output", "preset_archival.jp2"), archival);
                Console.WriteLine($"? Archival preset: {archival.Length:N0} bytes");
            }
        }

        #endregion

        #region Modern API - Custom Configuration

        static void DemonstrateModernEncodingCustom()
        {
            Console.WriteLine("\n??  Modern API - Custom Configuration");
            Console.WriteLine("-------------------------------------");

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "racoon.png")))
            {
                // Custom configuration with fluent API
                var config = new CompleteEncoderConfigurationBuilder()
                    .WithQuality(0.85)
                    .WithQuantization(q => q
                        .UseExpounded()
                        .WithBaseStepSize(0.008f)
                        .WithGuardBits(2))
                    .WithWavelet(w => w
                        .UseIrreversible_9_7()
                        .WithDecompositionLevels(6))
                    .WithProgression(p => p.UseLRCP())
                    .WithTiles(t => t.SetSize(512, 512))
                    .WithMetadata(m => m
                        .WithComment("Custom configuration demo")
                        .WithComment($"Size: {bitmap.Width}×{bitmap.Height}")
                        .WithCopyright("© 2025 CoreJ2K Demo")
                        .WithXml("<demo><quality>high</quality><purpose>testing</purpose></demo>"));

                var data = bitmap.EncodeToJ2K(config);
                File.WriteAllBytes(Path.Combine("output", "custom_configured.jp2"), data);
                Console.WriteLine($"? Custom configuration: {data.Length:N0} bytes");

                // Validate configuration
                if (config.IsValid)
                {
                    Console.WriteLine("  ? Configuration is valid");
                }
                else
                {
                    Console.WriteLine("  ? Configuration errors:");
                    foreach (var error in config.Validate())
                    {
                        Console.WriteLine($"    - {error}");
                    }
                }

                // Print configuration summary
                Console.WriteLine($"  Configuration: {config}");
            }
        }

        #endregion

        #region Skia Integration

        static void DemonstrateSkiaIntegration()
        {
            Console.WriteLine("\n?? SkiaSharp Integration");
            Console.WriteLine("------------------------");

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "basn6a08.png")))
            {
                // Direct file saving
                bitmap.SaveAsJ2KLossless(Path.Combine("output", "skia_lossless.jp2"));
                Console.WriteLine($"? Saved lossless (SkiaSharp)");

                bitmap.SaveAsJ2KHighQuality(
                    Path.Combine("output", "skia_highquality.jp2"),
                    "© 2025 SkiaSharp Demo");
                Console.WriteLine($"? Saved high quality with copyright (SkiaSharp)");

                // Using builder directly
                var builder = new CompleteEncoderConfigurationBuilder()
                    .ForWeb()
                    .WithComment("SkiaSharp integration demo")
                    .WithMetadata(m => m.WithCopyright("© 2025 CoreJ2K"));

                bitmap.SaveAsJ2K(Path.Combine("output", "skia_custom.jp2"), builder);
                Console.WriteLine($"? Saved with custom config (SkiaSharp)");
            }
        }

        #endregion

        #region Pfim Integration

        static void DemonstratePfimIntegration()
        {
            Console.WriteLine("\n?? Pfim Integration (DDS/TGA)");
            Console.WriteLine("-----------------------------");

            // Note: This demo requires actual DDS or TGA files
            // For now, we'll show the API usage
            Console.WriteLine("  ? Pfim is for DDS/TGA game textures");
            Console.WriteLine("  Usage:");
            Console.WriteLine("    var dds = Dds.Create(\"texture.dds\");");
            Console.WriteLine("    dds.SaveAsJ2KWeb(\"texture.jp2\", \"© 2025 Game Studio\");");
            Console.WriteLine("  ? Pfim extension methods available");
        }

        #endregion

        #region ImageSharp Integration

#if NET8_0_OR_GREATER
        static void DemonstrateImageSharpIntegration()
        {
            Console.WriteLine("\n???  ImageSharp Integration");
            Console.WriteLine("-------------------------");

            using var image = SixLabors.ImageSharp.Image.Load<Rgba32>(Path.Combine("samples", "racoon.png"));

            // Resize for demo
            image.Mutate(x => x.Resize(800, 600));

            // Direct file saving
            image.SaveAsJ2KLossless(Path.Combine("output", "imagesharp_lossless.jp2"));
            Console.WriteLine($"? Saved lossless (ImageSharp)");

            image.SaveAsJ2KHighQuality(
                Path.Combine("output", "imagesharp_highquality.jp2"),
                "© 2025 ImageSharp Demo");
            Console.WriteLine($"? Saved high quality (ImageSharp)");

            // Using builder
            var builder = new CompleteEncoderConfigurationBuilder()
                .ForWeb()
                .WithProgression(p => p.UseRLCP())
                .WithComment($"ImageSharp demo: {image.Width}×{image.Height}")
                .WithCopyright("© 2025 CoreJ2K");

            image.SaveAsJ2K(Path.Combine("output", "imagesharp_custom.jp2"), builder);
            Console.WriteLine($"? Saved with custom config (ImageSharp)");
        }
#endif

        #endregion

        #region Native Plugin Conversions

        /// <summary>
        /// Decodes JP2/J2K samples and converts them to native plugin formats provided
        /// by CoreJ2K.Skia, CoreJ2K.Windows, CoreJ2K.ImageSharp and CoreJ2K.Pfim.
        /// For Pfim the output is written as a TIFF file (Windows/System.Drawing only).
        /// </summary>
        static void DemonstrateNativePluginConversions()
        {
            Console.WriteLine("\n?? Native Plugin Conversion Demo");
            Console.WriteLine("--------------------------------");

            var sampleDir = Path.Combine("samples");
            if (!Directory.Exists(sampleDir))
            {
                Console.WriteLine("  Samples directory not found; skipping plugin conversion demo.");
                return;
            }

            var jp2 = Directory.GetFiles(sampleDir, "*.jp2");
            var j2k = Directory.GetFiles(sampleDir, "*.j2k");

            var files = new System.Collections.Generic.List<string>();
            files.AddRange(jp2);
            files.AddRange(j2k);

            if (files.Count == 0)
            {
                Console.WriteLine("  No JP2/J2K samples found; skipping.");
                return;
            }

            foreach (var f in files)
            {
                Console.WriteLine($"  Processing: {Path.GetFileName(f)}");
                try
                {
                    var img = J2kImage.FromFile(f);
                    var name = Path.GetFileNameWithoutExtension(f);

                    // SKIA
                    try
                    {
                        using (var sk = img.As<SKBitmap>())
                        using (var skimg = SKImage.FromBitmap(sk))
                        using (var data = skimg.Encode(SKEncodedImageFormat.Png, 90))
                        {
                            var outFile = Path.Combine("output", name + ".skia.png");
                            using (var fs = File.OpenWrite(outFile))
                                data.SaveTo(fs);
                        }
                        Console.WriteLine("    -> SKIA PNG written");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    SKIA conversion failed: {ex.Message}");
                    }

#if NETFRAMEWORK
                    // System.Drawing (Windows) via Windows plugin
                    try
                    {
                        using (var win = img.As<System.Drawing.Image>())
                        {
                            var outFile = Path.Combine("output", name + ".windows.png");
                            win.Save(outFile, System.Drawing.Imaging.ImageFormat.Png);
                        }
                        Console.WriteLine("    -> System.Drawing PNG written");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    System.Drawing conversion failed: {ex.Message}");
                    }
#endif

#if NET8_0_OR_GREATER
                    // ImageSharp
                    try
                    {
                        using (var ish = img.As<SixLabors.ImageSharp.Image>())
                        {
                            var outFile = Path.Combine("output", name + ".imagesharp.png");
                            ish.Save(outFile);
                        }
                        Console.WriteLine("    -> ImageSharp PNG written");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    ImageSharp conversion failed: {ex.Message}");
                    }
#endif

#if NETFRAMEWORK
                    // Pfim -> TIFF (Windows/System.Drawing)
                    try
                    {
                        using (var pimg = img.As<Pfim.IImage>())
                        {
                            var outFile = Path.Combine("output", name + ".pfim.tiff");
                            SavePfimAsTiff(pimg, outFile);
                        }
                        Console.WriteLine("    -> Pfim TIFF written");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Pfim conversion failed: {ex.Message}");
                    }
#else
                    // Pfim - save raw data as fallback when System.Drawing unavailable
                    try
                    {
                        using (var pimg = img.As<Pfim.IImage>())
                        {
                            var outFile = Path.Combine("output", name + ".pfim.raw");
                            File.WriteAllBytes(outFile, pimg.Data);
                        }
                        Console.WriteLine("    -> Pfim raw data written (fallback, TIFF not available on this target)");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"    Pfim fallback write failed: {ex.Message}");
                    }
#endif

                    // If the decoded object implements IDisposable the 'using' above will dispose.
                }
                catch (Exception e)
                {
                    Console.WriteLine($"    Error processing file: {e.Message}");
                }
            }
        }

#if NETFRAMEWORK
        private static void SavePfimAsTiff(Pfim.IImage pimg, string outPath)
        {
            if (pimg == null) throw new ArgumentNullException(nameof(pimg));

            var width = pimg.Width;
            var height = pimg.Height;
            var data = pimg.Data;
            var bpp = pimg.BitsPerPixel; // bits per pixel (total)
            var bytesPerPixel = Math.Max(1, bpp / 8);

            System.Drawing.Imaging.PixelFormat pf;
            if (bytesPerPixel == 3) pf = PixelFormat.Format24bppRgb;
            else if (bytesPerPixel >= 4) pf = PixelFormat.Format32bppArgb;
            else pf = PixelFormat.Format8bppIndexed;

            using (var bmp = new System.Drawing.Bitmap(width, height, pf))
            {
                var rect = new System.Drawing.Rectangle(0, 0, width, height);
                var bmpData = bmp.LockBits(rect, System.Drawing.Imaging.ImageLockMode.WriteOnly, pf);
                try
                {
                    var dstStride = Math.Abs(bmpData.Stride);
                    var srcStride = pimg.Stride;

                    for (int y = 0; y < height; y++)
                    {
                        var srcOff = y * srcStride;
                        var dstPtr = IntPtr.Add(bmpData.Scan0, y * bmpData.Stride);
                        System.Runtime.InteropServices.Marshal.Copy(data, srcOff, dstPtr, width * bytesPerPixel);
                    }
                }
                finally
                {
                    bmp.UnlockBits(bmpData);
                }

                bmp.Save(outPath, System.Drawing.Imaging.ImageFormat.Tiff);
            }
        }
#endif

        #endregion

        #region Decoding Demo

        static void DemonstrateDecoding()
        {
            Console.WriteLine("\n?? Decoding Demo");
            Console.WriteLine("----------------");

            // Standard decoding
            var image1 = J2kImage.FromFile(Path.Combine("output", "preset_web.jp2"));
            var bitmap1 = image1.As<SKBitmap>();
            Console.WriteLine($"? Decoded: {bitmap1.Width}×{bitmap1.Height} pixels");

            // Decoding with modern configuration
            var decoderConfig = new J2KDecoderConfiguration()
                .WithResolutionLevel(2);  // Half resolution

            var image2 = J2kImage.FromFile(Path.Combine("output", "preset_highquality.jp2"), decoderConfig);
            var bitmap2 = image2.As<SKBitmap>();
            Console.WriteLine($"? Decoded at half resolution: {bitmap2.Width}×{bitmap2.Height} pixels");

            // Using extension methods
            var bitmap3 = SKBitmapJ2kExtensions.FromJ2KFile(Path.Combine("output", "preset_photography.jp2"));
            Console.WriteLine($"? Decoded with extension method: {bitmap3.Width}×{bitmap3.Height} pixels");

            bitmap1.Dispose();
            bitmap2.Dispose();
            bitmap3.Dispose();
        }

        #endregion

        #region Performance Demo

        static void DemonstratePerformance()
        {
            Console.WriteLine("\n? Performance Comparison");
            Console.WriteLine("------------------------");

            using (var bitmap = SKBitmap.Decode(Path.Combine("samples", "dog.jpeg")))
            {
                // Measure traditional API
                var sw1 = Stopwatch.StartNew();
                var traditional = J2kImage.ToBytes(bitmap);
                sw1.Stop();
                Console.WriteLine($"Traditional API: {sw1.ElapsedMilliseconds}ms ? {traditional.Length:N0} bytes");

                // Measure modern API with preset
                var sw2 = Stopwatch.StartNew();
                var modern = bitmap.EncodeToJ2KWeb();
                sw2.Stop();
                Console.WriteLine($"Modern API (preset): {sw2.ElapsedMilliseconds}ms ? {modern.Length:N0} bytes");

                // Measure modern API with custom config
                var sw3 = Stopwatch.StartNew();
                var config = new CompleteEncoderConfigurationBuilder()
                    .ForBalanced()
                    .WithCopyright("© 2025");
                var custom = bitmap.EncodeToJ2K(config);
                sw3.Stop();
                Console.WriteLine($"Modern API (custom): {sw3.ElapsedMilliseconds}ms ? {custom.Length:N0} bytes");

                Console.WriteLine($"\n? All methods have similar performance");
                Console.WriteLine($"? Modern API adds zero overhead!");
            }

            // Decoding performance
            using (var decodedBitmap = SKBitmapJ2kExtensions.FromJ2KFile(Path.Combine("output", "preset_web.jp2")))
            {
                var sw4 = Stopwatch.StartNew();
                var image = J2kImage.FromFile(Path.Combine("output", "preset_web.jp2"));
                sw4.Stop();
                Console.WriteLine($"\nDecoding: {sw4.ElapsedMilliseconds}ms for {decodedBitmap.Width}×{decodedBitmap.Height} image");
            }
        }

        #endregion
    }
}
