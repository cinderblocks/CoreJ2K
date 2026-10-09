// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.IO;
using System.Linq;
using System.Text;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// The ways a JP2 file's box lengths may be unusual and still hold a perfectly good image: a codestream box of length 0 ("to the end of
    /// the file"), a box whose length is written as 64 bits, bytes after the last box, and a final box that is longer than the file
    /// because the file was cut short.
    /// </summary>
    public class Jp2BoxLengthTests
    {
        private const int Width = 36, Height = 28;

        private static (byte[] Jp2, int[] Plane) Encode()
        {
            var rnd = new Random(3);
            var plane = Enumerable.Range(0, Width * Height).Select(_ => rnd.Next(256)).ToArray();
            var source = new InterleavedImageSource(Width, Height, 1, 8, new[] { false }, new[] { plane.Select(v => v - 128).ToArray() });
            return (J2kImage.ToBytes(source, new J2KEncoderConfiguration().WithLossless().WithFileFormat(true).ToParameterList()), plane);
        }

        /// <summary>The offset of the box of the given type among the top-level boxes of the file.</summary>
        private static int BoxOffset(byte[] jp2, string type)
        {
            for (var pos = 0; pos + 8 <= jp2.Length;)
            {
                var length = (jp2[pos] << 24) | (jp2[pos + 1] << 16) | (jp2[pos + 2] << 8) | jp2[pos + 3];
                if (Encoding.ASCII.GetString(jp2, pos + 4, 4) == type) return pos;
                pos += length;
            }
            throw new InvalidOperationException($"No {type} box.");
        }

        [Fact]
        public void FinalBoxLongerThanTheFile_StillDecodes()
        {
            var (jp2, plane) = Encode();
            // the EOC marker is the last two bytes: without it all the packets are there, and the codestream box claims two more bytes
            var cut = jp2.Take(jp2.Length - 2).ToArray();

            Assert.Equal(plane, J2kImage.FromBytes(cut).GetComponent(0));
        }

        [Fact]
        public void CodestreamBoxOfLengthZero_ExtendsToTheEndOfTheFile()
        {
            var (jp2, plane) = Encode();
            var patched = (byte[])jp2.Clone();
            var box = BoxOffset(patched, "jp2c");
            patched[box] = patched[box + 1] = patched[box + 2] = patched[box + 3] = 0;

            Assert.Equal(plane, J2kImage.FromBytes(patched).GetComponent(0));
        }

        [Fact]
        public void BytesAfterTheLastBox_AreIgnored()
        {
            var (jp2, plane) = Encode();
            var padded = jp2.Concat(new byte[] { 0x12, 0x34 }).ToArray();

            Assert.Equal(plane, J2kImage.FromBytes(padded).GetComponent(0));
        }

        [Fact]
        public void BoxWithA64BitLength_IsReadAndSkipped()
        {
            var (jp2, plane) = Encode();
            var content = Encoding.UTF8.GetBytes("<note>64-bit length</note>");
            var xml = new byte[16 + content.Length];
            xml[3] = 1; // LBox = 1: the length is in the 8 bytes after the type
            Encoding.ASCII.GetBytes("xml ").CopyTo(xml, 4);
            var total = (long)xml.Length;
            for (var i = 0; i < 8; i++) xml[8 + i] = (byte)(total >> (8 * (7 - i)));
            content.CopyTo(xml, 16);

            var at = BoxOffset(jp2, "jp2c");
            var withXml = jp2.Take(at).Concat(xml).Concat(jp2.Skip(at)).ToArray();

            using var stream = new MemoryStream(withXml);
            var image = J2kImage.FromStream(stream, out var metadata);
            Assert.Equal(plane, image.GetComponent(0));
            Assert.Single(metadata.XmlBoxes);
            Assert.Equal("<note>64-bit length</note>", metadata.XmlBoxes[0].XmlContent);
        }
    }
}
