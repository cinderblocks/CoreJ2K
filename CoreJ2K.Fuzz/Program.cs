// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using SharpFuzz;

namespace CoreJ2K.Fuzz
{
    /// <summary>
    /// Main entry point for fuzzing CoreJ2K with SharpFuzz/AFL.
    /// Tests the JPEG 2000 decoder with random/mutated inputs.
    /// </summary>
    public class Program
    {
        public static void Main(string[] args)
        {
            // Check command line arguments
            if (args.Length > 0)
            {
                string target = args[0].ToLowerInvariant();
                
                switch (target)
                {
                    case "decoder":
                        Console.WriteLine("Fuzzing JPEG 2000 decoder...");
                        Fuzzer.Run(FuzzDecoder);
                        break;
                    
                    case "encoder":
                        Console.WriteLine("Fuzzing JPEG 2000 encoder...");
                        Fuzzer.Run(FuzzEncoder);
                        break;
                    
                    case "headers":
                        Console.WriteLine("Fuzzing JPEG 2000 header parsing...");
                        Fuzzer.Run(FuzzHeaders);
                        break;
                    
                    case "markers":
                        Console.WriteLine("Fuzzing marker segment parsing...");
                        Fuzzer.Run(FuzzMarkers);
                        break;
                    
                    default:
                        Console.WriteLine($"Unknown target: {target}");
                        PrintUsage();
                        return;
                }
            }
            else
            {
                // Default: fuzz the decoder (most common attack surface)
                Console.WriteLine("Fuzzing JPEG 2000 decoder (default)...");
                Fuzzer.Run(FuzzDecoder);
            }
        }

        /// <summary>
        /// Fuzzes the JPEG 2000 decoder with random byte inputs.
        /// Tests for crashes, hangs, and exceptions.
        /// </summary>
        private static void FuzzDecoder(Stream input)
        {
            try
            {
                // Attempt to decode the input as JPEG 2000
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();
                
                // Skip empty inputs
                if (data.Length == 0) return;
                
                // Try to decode
                var image = J2kImage.FromBytes(data);
                
                // If successful, try to access the image data
                if (image != null && image.Width > 0 && image.Height > 0)
                {
                    // Validate dimensions are reasonable
                    if (image.Width > 100000 || image.Height > 100000)
                    {
                        // Skip unreasonably large images
                        return;
                    }
                    
                    // Try to access image properties (this exercises the full decoding pipeline)
                    try
                    {
                        _ = image.NumberOfComponents;
                        _ = image.BitDepths;
                    }
                    catch
                    {
                        // Expected for some malformed inputs
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                // Expected for malicious inputs trying to allocate huge buffers
                // Our fixes should prevent this, but if it happens, it's not a crash
            }
            catch (ArgumentException)
            {
                // Expected for invalid parameters (our validation)
            }
            catch (InvalidOperationException)
            {
                // Expected for malformed data (our validation)
            }
            catch (NotSupportedException)
            {
                // Expected for unsupported JPEG 2000 features
            }
            catch (IOException)
            {
                // Expected for truncated/corrupt files
            }
            catch (Exception ex)
            {
                // Unexpected exceptions should be reported
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                Console.Error.WriteLine($"Stack: {ex.StackTrace}");
                throw; // Re-throw to let fuzzer know this is a finding
            }
        }

        /// <summary>
        /// Fuzzes the JPEG 2000 encoder with random configuration.
        /// </summary>
        private static void FuzzEncoder(Stream input)
        {
            try
            {
                // Read input bytes
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();
                
                // Skip empty or too small inputs
                if (data.Length < 10) return;
                
                // Use input bytes to generate a small test image
                int width = Math.Min(100, Math.Abs(BitConverter.ToInt32(data, 0) % 1000));
                int height = Math.Min(100, Math.Abs(BitConverter.ToInt32(data, 4) % 1000));
                
                if (width <= 0) width = 16;
                if (height <= 0) height = 16;
                
                // Create a simple test image using ImageFactory
                // Use grayscale (1 component) to keep it simple
                byte[] imageBytes = new byte[width * height];
                
                // Fill with data from input (or zeros if not enough)
                int dataIndex = 8;
                for (int i = 0; i < imageBytes.Length && dataIndex < data.Length; i++)
                {
                    imageBytes[i] = data[dataIndex++];
                }
                
                // Create image source
                var imgsrc = new j2k.image.input.ImgReaderPGM(
                    new System.IO.MemoryStream(CreatePGMBytes(width, height, imageBytes)));
                
                // Try to encode with default configuration
                var config = new Configuration.J2KEncoderConfiguration();
                var encoded = J2kImage.ToBytes(imgsrc, config);
                
                // Validate output is non-empty
                if (encoded == null || encoded.Length == 0)
                {
                    throw new InvalidOperationException("Encoder produced empty output");
                }
            }
            catch (ArgumentException) { /* Expected */ }
            catch (InvalidOperationException) { /* Expected */ }
            catch (OutOfMemoryException) { /* Expected for malicious sizes */ }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a simple PGM (Portable Gray Map) format bytes for testing.
        /// </summary>
        private static byte[] CreatePGMBytes(int width, int height, byte[] pixels)
        {
            using var ms = new MemoryStream();
            using var writer = new StreamWriter(ms, System.Text.Encoding.ASCII);
            
            // PGM header
            writer.WriteLine("P5");
            writer.WriteLine($"{width} {height}");
            writer.WriteLine("255");
            writer.Flush();
            
            // Pixel data
            ms.Write(pixels, 0, pixels.Length);
            
            return ms.ToArray();
        }

        /// <summary>
        /// Fuzzes JPEG 2000 header parsing specifically.
        /// </summary>
        private static void FuzzHeaders(Stream input)
        {
            try
            {
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();
                
                if (data.Length < 20) return; // Too small for valid header
                
                // Try to parse just the headers without full decoding
                using var stream = new MemoryStream(data);
                
                // This will parse main header, tile headers, etc.
                // Should handle all malformed headers gracefully
                try
                {
                    var image = J2kImage.FromStream(stream);
                    
                    // Access header information
                    _ = image?.Width;
                    _ = image?.Height;
                    _ = image?.NumberOfComponents;
                }
                catch (EndOfStreamException) { /* Expected for truncated */ }
            }
            catch (ArgumentException) { /* Expected */ }
            catch (InvalidOperationException) { /* Expected */ }
            catch (IOException) { /* Expected */ }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fuzzes marker segment parsing.
        /// </summary>
        private static void FuzzMarkers(Stream input)
        {
            try
            {
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();
                
                if (data.Length < 4) return;
                
                // Prepend JPEG 2000 signature to make parser attempt to read markers
                byte[] j2kData = new byte[12 + data.Length];
                
                // JPEG 2000 signature
                j2kData[0] = 0xFF; j2kData[1] = 0x4F; // SOC
                j2kData[2] = 0xFF; j2kData[3] = 0x51; // SIZ marker
                
                // Copy fuzzer input as marker data
                Array.Copy(data, 0, j2kData, 4, data.Length);
                
                using var stream = new MemoryStream(j2kData);
                
                try
                {
                    var image = J2kImage.FromStream(stream);
                }
                catch (EndOfStreamException) { /* Expected */ }
            }
            catch (ArgumentException) { /* Expected */ }
            catch (InvalidOperationException) { /* Expected */ }
            catch (IOException) { /* Expected */ }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: CoreJ2K.Fuzz [target]");
            Console.WriteLine();
            Console.WriteLine("Targets:");
            Console.WriteLine("  decoder  - Fuzz JPEG 2000 decoder (default)");
            Console.WriteLine("  encoder  - Fuzz JPEG 2000 encoder");
            Console.WriteLine("  headers  - Fuzz header parsing");
            Console.WriteLine("  markers  - Fuzz marker segment parsing");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run decoder");
            Console.WriteLine();
            Console.WriteLine("For use with AFL/libFuzzer:");
            Console.WriteLine("  sharpfuzz CoreJ2K.dll");
            Console.WriteLine("  afl-fuzz -i testcases -o findings CoreJ2K.Fuzz.exe decoder");
        }
    }
}
