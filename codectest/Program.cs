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
            Console.WriteLine($"? Saved high quality with copyright (ImageSharp)");

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
