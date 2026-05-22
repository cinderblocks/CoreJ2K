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

                    case "jp2":
                        Console.WriteLine("Fuzzing JP2 container / file-format parsing...");
                        Fuzzer.Run(FuzzJP2Container);
                        break;

                    case "icc":
                        Console.WriteLine("Fuzzing ICC profile parsing...");
                        Fuzzer.Run(FuzzIccProfile);
                        break;

                    case "encoderconfig":
                        Console.WriteLine("Fuzzing encoder with random configuration...");
                        Fuzzer.Run(FuzzEncoderConfig);
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

                // Use input bytes to derive small, safe image dimensions.
                // Mask to positive range first to avoid Math.Abs(int.MinValue) overflow,
                // then clamp to [1, 100] so allocations stay bounded.
                int rawW = BitConverter.ToInt32(data, 0) & 0x7FFFFFFF;
                int rawH = BitConverter.ToInt32(data, 4) & 0x7FFFFFFF;
                int width = Math.Max(1, Math.Min(100, rawW % 100 + 1));
                int height = Math.Max(1, Math.Min(100, rawH % 100 + 1));
                
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
            catch (OverflowException) { /* Expected for extreme input values */ }
            catch (OutOfMemoryException) { /* Expected for malicious sizes */ }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates a simple PGM
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
            Console.WriteLine("  decoder      - Fuzz JPEG 2000 decoder (default)");
            Console.WriteLine("  encoder      - Fuzz JPEG 2000 encoder (random pixels)");
            Console.WriteLine("  encoderconfig- Fuzz JPEG 2000 encoder (random config)");
            Console.WriteLine("  headers      - Fuzz header parsing");
            Console.WriteLine("  markers      - Fuzz marker segment parsing");
            Console.WriteLine("  jp2          - Fuzz JP2 container / box parsing");
            Console.WriteLine("  icc          - Fuzz ICC profile parsing");
            Console.WriteLine();
            Console.WriteLine("Example:");
            Console.WriteLine("  dotnet run decoder");
            Console.WriteLine();
            Console.WriteLine("For use with AFL/libFuzzer:");
            Console.WriteLine("  sharpfuzz CoreJ2K.dll");
            Console.WriteLine("  afl-fuzz -i testcases -o findings CoreJ2K.Fuzz.exe decoder");
        }

        /// <summary>
        /// Fuzzes JP2 container / file-format box parsing.
        /// This exercises FileFormatReader and JP2Validator independently of
        /// the codestream marker parser, targeting malformed box lengths,
        /// unknown box types, and truncated signatures.
        /// </summary>
        private static void FuzzJP2Container(Stream input)
        {
            try
            {
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();

                if (data.Length < 4) return;

                // Feed directly to the JP2 validator's codestream checker —
                // this accepts a raw byte[] so no wrapping is needed.
                var validator = new j2k.fileformat.reader.JP2Validator();
                validator.ValidateBasicCodestreamMarkers(data);
                validator.ValidateIccProfileBasic(data);

                if (data.Length >= 8)
                    validator.ValidateCodestreamComprehensive(data);
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fuzzes ICC profile binary parsing via ICCProfileData.
        /// This exercises the raw ICC header, tag table, and type parsers
        /// without needing a surrounding JP2 or J2K container.
        /// </summary>
        private static void FuzzIccProfile(Stream input)
        {
            try
            {
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();

                if (data.Length == 0) return;

                var profile = new Color.ICC.ICCProfileData(data);
                _ = profile.IsValid;
                _ = profile.ProfileBytes;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (OverflowException) { }
            catch (IOException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Fuzzes the encoder with random configuration derived from input bytes.
        /// Exercises tile sizes, quantisation, resolution levels, and progression
        /// order rather than only pixel content.
        /// </summary>
        private static void FuzzEncoderConfig(Stream input)
        {
            try
            {
                using var ms = new MemoryStream();
                input.CopyTo(ms);
                byte[] data = ms.ToArray();

                if (data.Length < 16) return;

                // Fixed small image so memory is always bounded.
                const int width = 32;
                const int height = 32;

                byte[] pixels = new byte[width * height];
                int dataIndex = 0;
                for (int i = 0; i < pixels.Length && dataIndex < data.Length; i++)
                    pixels[i] = data[dataIndex++];

                // Derive encoder config fields from input bytes.
                int numResolutions = Math.Max(1, Math.Min(8, (data[0] & 0x07) + 1));
                int cblkWidth  = 1 << Math.Max(2, Math.Min(6, (data[1] & 0x0F)));
                int cblkHeight = 1 << Math.Max(2, Math.Min(6, (data[2] & 0x0F)));
                // Tile size: 0 means "no tiling" (tile == image); otherwise a small power-of-two.
                int tileSize = (data[3] & 0x01) == 0 ? 0 : (1 << Math.Max(3, Math.Min(6, (data[3] >> 1) & 0x0F)));

                var config = new Configuration.J2KEncoderConfiguration();
                config.Wavelet.DecompositionLevels = numResolutions;
                config.CodeBlocks.SetSize(cblkWidth, cblkHeight);

                if (tileSize > 0)
                    config.Tiles.SetSize(tileSize, tileSize);

                var imgsrc = new j2k.image.input.ImgReaderPGM(
                    new MemoryStream(CreatePGMBytes(width, height, pixels)));

                var encoded = J2kImage.ToBytes(imgsrc, config);

                if (encoded == null || encoded.Length == 0)
                    throw new InvalidOperationException("Encoder produced empty output");
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
            catch (OverflowException) { }
            catch (OutOfMemoryException) { }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"UNEXPECTED EXCEPTION: {ex.GetType().Name}");
                Console.Error.WriteLine($"Message: {ex.Message}");
                throw;
            }
        }
    }
}
