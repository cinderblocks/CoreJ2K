// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using CoreJ2K.Util;

namespace CoreJ2K.Tests.Conformance
{
    /// <summary>
    /// A synthetic image of any component count, bit depth and signedness, for round trips and for exchange with other codecs.
    /// Samples are kept "centred": an unsigned sample v is stored as v - 2^(bits-1) and a signed one as itself, which is what the
    /// encoder takes. <see cref="OffsetBinary"/> gives the unsigned form that <see cref="J2kImage"/> returns on decoding.
    /// </summary>
    internal sealed class SampleImage
    {
        public SampleImage(int width, int height, int bits, bool signed, int[][] centred)
        {
            Width = width;
            Height = height;
            Bits = bits;
            Signed = signed;
            Centred = centred;
        }

        public int Width { get; }
        public int Height { get; }
        public int Bits { get; }
        public bool Signed { get; }
        public int Components => Centred.Length;
        public int[][] Centred { get; }
        public int Half => 1 << (Bits - 1);

        public InterleavedImageSource ToSource() =>
            new InterleavedImageSource(Width, Height, Components, Bits, Enumerable.Repeat(Signed, Components).ToArray(), Centred);

        /// <summary>Every sample as an unsigned value, which is how <see cref="J2kImage.GetComponent"/> returns them.</summary>
        public int[][] OffsetBinary() => Centred.Select(c => c.Select(v => v + Half).ToArray()).ToArray();

        /// <summary>The samples as the file formats of other tools hold them: unsigned values, or signed ones for a signed image.</summary>
        public int[][] Native() => Signed ? Centred : OffsetBinary();

        /// <summary>Planar raw samples, one byte or two (big-endian) each, as <c>opj_compress -F</c> reads them.</summary>
        public byte[] ToRaw()
        {
            // a signed sample is sign-extended to the byte or word that holds it
            var wide = Bits > 8;
            using var stream = new MemoryStream();
            foreach (var comp in Native())
            {
                foreach (var v in comp)
                {
                    if (wide) stream.WriteByte((byte)(v >> 8));
                    stream.WriteByte((byte)v);
                }
            }
            return stream.ToArray();
        }

        // ---- generation -------------------------------------------------------------------------------------------

        /// <summary>Content styles that stress different parts of the codec.</summary>
        private const int Styles = 8;

        public static SampleImage Generate(Random rnd, int width, int height, int components, int bits, bool signed)
        {
            var lo = signed ? -(1 << (bits - 1)) : 0;
            var hi = signed ? (1 << (bits - 1)) - 1 : (1 << bits) - 1;
            var half = 1 << (bits - 1);
            var centred = new int[components][];
            for (var c = 0; c < components; c++)
            {
                var style = rnd.Next(Styles);
                var phase = rnd.NextDouble() * 6;
                var scale = 3 + rnd.NextDouble() * 20;
                var blocks = new int[16];
                for (var i = 0; i < blocks.Length; i++) blocks[i] = rnd.Next(lo, hi + 1);
                var plane = new int[width * height];
                for (var y = 0; y < height; y++)
                {
                    for (var x = 0; x < width; x++)
                    {
                        long v;
                        switch (style)
                        {
                            case 0: // smooth
                                v = (long)(lo + (hi - lo) * (0.5 + 0.45 * Math.Sin(x / scale + phase) * Math.Cos(y / (scale + 2))));
                                break;
                            case 1: // gradient with a little noise
                                v = lo + (long)(hi - lo) * (x + y) / Math.Max(1, width + height) + rnd.Next(-2, 3);
                                break;
                            case 2: // flat blocks with hard edges
                                v = blocks[((x * 4 / Math.Max(1, width)) + 4 * (y * 4 / Math.Max(1, height))) & 15];
                                break;
                            case 3: // full-range noise
                                v = rnd.Next(lo, hi + 1);
                                break;
                            case 4: // constant
                                v = blocks[0];
                                break;
                            case 5: // extremes
                                v = ((x + y) & 1) == 0 ? lo : hi;
                                break;
                            case 6: // mostly flat, a few spikes
                                v = rnd.Next(60) == 0 ? rnd.Next(lo, hi + 1) : lo + (hi - lo) / 3;
                                break;
                            default: // smooth plus noise
                                v = (long)(lo + (hi - lo) * (0.5 + 0.35 * Math.Sin(x / scale + phase))) + rnd.Next(-(hi - lo) / 40 - 1, (hi - lo) / 40 + 2);
                                break;
                        }
                        plane[y * width + x] = (int)Math.Clamp(v, lo, hi) - (signed ? 0 : half);
                    }
                }
                centred[c] = plane;
            }
            return new SampleImage(width, height, bits, signed, centred);
        }

        // ---- PGX (one file per component) ------------------------------------------------------------------------

        /// <summary>
        /// Reads a PGX file (<c>PG ML +|- bits width height</c> and big-endian samples) and returns the samples as unsigned values,
        /// adding half the range to signed ones.
        /// </summary>
        public static (int Width, int Height, int Bits, bool Signed, int[] OffsetBinary) ReadPgx(string path)
        {
            var bytes = File.ReadAllBytes(path);
            var eol = Array.IndexOf(bytes, (byte)'\n');
            var header = System.Text.Encoding.ASCII.GetString(bytes, 0, eol).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (header.Length < 6 || header[0] != "PG")
                throw new InvalidDataException($"Not a PGX file: {path}");
            var little = header[1] == "LM";
            var signed = header[2] == "-";
            var bits = int.Parse(header[3]);
            var width = int.Parse(header[4]);
            var height = int.Parse(header[5]);
            var wide = bits > 8;
            var half = 1 << (bits - 1);
            var samples = new int[width * height];
            var p = eol + 1;
            for (var i = 0; i < samples.Length; i++)
            {
                int v;
                if (wide)
                {
                    v = little ? bytes[p] | (bytes[p + 1] << 8) : (bytes[p] << 8) | bytes[p + 1];
                    p += 2;
                }
                else
                {
                    v = bytes[p++];
                }
                if (signed)
                {
                    // two's complement in the byte or word that holds the sample
                    v = wide ? (short)v : (sbyte)v;
                    v += half;
                }
                samples[i] = v;
            }
            return (width, height, bits, signed, samples);
        }
    }
}
