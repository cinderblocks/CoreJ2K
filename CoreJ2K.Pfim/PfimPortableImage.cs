// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using Pfim;
using CoreJ2K.Util;
using J2KIImage = CoreJ2K.Util.IImage;
using PfIImage = Pfim.IImage;

namespace CoreJ2K.Pfim
{
    /// <summary>
    /// Minimal Pfim-backed image implementing CoreJ2K.Util.IImage for decode targets.
    /// Wraps a raw byte buffer with width/height/format so user code can get a Pfim IImage via As&lt;IImage&gt;().
    /// </summary>
    internal sealed class PfimPortableImage : ImageBase<PfIImage>
    {
        private readonly ImageFormat _format;
        private readonly int _bitsPerPixel;

        internal PfimPortableImage(int width, int height, ImageFormat format, byte[] bytes, int bitsPerPixel)
            : base(width, height, ComponentsFor(format), bytes)
        {
            _format = format;
            _bitsPerPixel = bitsPerPixel;
        }

        protected override object GetImageObject()
        {
            // Wrap the byte buffer in a lightweight Pfim.IImage implementation.
            return new RawPfimImage(Width, Height, _format, Bytes, _bitsPerPixel);
        }

        private static int ComponentsFor(ImageFormat fmt)
        {
            switch (fmt)
            {
                case ImageFormat.Rgb24: return 3;
                case ImageFormat.Rgba32: return 4;
                default:
                    throw new NotSupportedException($"Unsupported Pfim format {fmt} for decode output.");
            }
        }

        // Simple Pfim.IImage wrapper for already-decoded interleaved bytes.
        private sealed class RawPfimImage : PfIImage, IDisposable
        {
            public RawPfimImage(int width, int height, ImageFormat fmt, byte[] data, int bitsPerPixel)
            {
                Width = width;
                Height = height;
                Format = fmt;
                Data = data ?? throw new ArgumentNullException(nameof(data));
                Stride = BitsPerPixelToStride(bitsPerPixel, width);
                BitsPerPixel = bitsPerPixel;
                BPP = Math.Max(1, bitsPerPixel / 8);
                Compressed = false;
                MipMaps = Array.Empty<MipMapOffset>();
            }

            public int Width { get; }
            public int Height { get; }
            public ImageFormat Format { get; }
            public byte[] Data { get; }
            public int Stride { get; }
            public int BPP { get; }

            // Implemented to satisfy Pfim.IImage
            public int DataLen => Data?.Length ?? 0;
            public int BitsPerPixel { get; private set; }
            public bool Compressed { get; private set; }
            public MipMapOffset[] MipMaps { get; private set; }

            private static int BitsPerPixelToStride(int bpp, int width) => (bpp / 8) * width;

            // Pfim.IImage methods - noop for already-decompressed buffer
            public void Decompress()
            {
                // already raw
            }

            public void ApplyColorMap()
            {
                // no-op: data already in target format
            }

            public void Dispose() { /* nothing to free */ }
        }
    }
}
