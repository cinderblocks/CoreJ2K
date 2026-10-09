// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// A JP2 header may hold several colour specification boxes, and a reader uses the first one it can use; the others are for readers
    /// that know other methods. CoreJ2K kept the last one, and refused a file in which any of them had a method it did not know.
    /// </summary>
    public class ColourSpecificationOrderTests
    {
        private const int Width = 32, Height = 24;

        private static byte[] Box(string type, params byte[][] content)
        {
            var body = content.SelectMany(c => c).ToArray();
            var box = new byte[8 + body.Length];
            var length = box.Length;
            box[0] = (byte)(length >> 24); box[1] = (byte)(length >> 16); box[2] = (byte)(length >> 8); box[3] = (byte)length;
            for (var i = 0; i < 4; i++) box[4 + i] = (byte)type[i];
            body.CopyTo(box, 8);
            return box;
        }

        private static byte[] EnumeratedColour(int colourSpace) =>
            Box("colr", new byte[] { 1, 0, 0, (byte)(colourSpace >> 24), (byte)(colourSpace >> 16), (byte)(colourSpace >> 8), (byte)colourSpace });

        /// <summary>A colour specification box of a method the reader has never heard of.</summary>
        private static byte[] UnknownMethodColour() => Box("colr", new byte[] { 3, 0, 0, 1, 2, 3, 4 });

        private static byte[] WithColourBoxes(byte[] jp2, params byte[][] colourBoxes)
        {
            var boxes = new List<(string Type, byte[] Bytes)>();
            for (var pos = 0; pos + 8 <= jp2.Length;)
            {
                var length = (jp2[pos] << 24) | (jp2[pos + 1] << 16) | (jp2[pos + 2] << 8) | jp2[pos + 3];
                var type = new string(new[] { (char)jp2[pos + 4], (char)jp2[pos + 5], (char)jp2[pos + 6], (char)jp2[pos + 7] });
                boxes.Add((type, jp2.Skip(pos).Take(length).ToArray()));
                pos += length;
            }

            using var output = new MemoryStream();
            foreach (var (type, bytes) in boxes)
            {
                if (type != "jp2h") { output.Write(bytes, 0, bytes.Length); continue; }

                var children = new List<byte[]>();
                for (var pos = 8; pos + 8 <= bytes.Length;)
                {
                    var length = (bytes[pos] << 24) | (bytes[pos + 1] << 16) | (bytes[pos + 2] << 8) | bytes[pos + 3];
                    var childType = new string(new[] { (char)bytes[pos + 4], (char)bytes[pos + 5], (char)bytes[pos + 6], (char)bytes[pos + 7] });
                    if (childType == "colr") { if (colourBoxes.Length > 0) { children.AddRange(colourBoxes); colourBoxes = Array.Empty<byte[]>(); } }
                    else children.Add(bytes.Skip(pos).Take(length).ToArray());
                    pos += length;
                }
                var header = Box("jp2h", children.ToArray());
                output.Write(header, 0, header.Length);
            }
            return output.ToArray();
        }

        private static (byte[] Jp2, int[][] Planes) Encode(CoreJ2K.j2k.fileformat.metadata.ChannelDefinitionData? definitions = null)
        {
            var rnd = new Random(6);
            var planes = Enumerable.Range(0, 3).Select(_ => Enumerable.Range(0, Width * Height).Select(i => rnd.Next(256)).ToArray()).ToArray();
            var source = new InterleavedImageSource(Width, Height, 3, 8, new bool[3], planes.Select(p => p.Select(v => v - 128).ToArray()).ToArray());
            var metadata = definitions == null ? null : new CoreJ2K.j2k.fileformat.metadata.J2KMetadata { ChannelDefinitions = definitions };
            return (J2kImage.ToBytes(source, metadata, new J2KEncoderConfiguration().WithLossless().WithFileFormat(true).ToParameterList()), planes);
        }

        private static int[][] Decode(byte[] file)
        {
            var image = J2kImage.FromBytes(file);
            return Enumerable.Range(0, image.NumberOfComponents).Select(image.GetComponent).ToArray();
        }

        [Fact]
        public void ColourBoxOfAnUnknownMethod_IsSkipped()
        {
            var (jp2, planes) = Encode();
            var decoded = Decode(WithColourBoxes(jp2, UnknownMethodColour(), EnumeratedColour(16)));

            for (var c = 0; c < 3; c++) Assert.Equal(planes[c], decoded[c]);
        }

        [Fact]
        public void FileWhoseOnlyColourBoxIsOfAnUnknownMethod_DecodesWithoutColourManagement()
        {
            var (jp2, planes) = Encode();
            var decoded = Decode(WithColourBoxes(jp2, UnknownMethodColour()));

            for (var c = 0; c < 3; c++) Assert.Equal(planes[c], decoded[c]);
        }

        [Fact]
        public void FirstUsableColourBox_Wins()
        {
            var (jp2, planes) = Encode();
            var syccOnly = Decode(WithColourBoxes(jp2, EnumeratedColour(18)));
            Assert.NotEqual(planes[0], syccOnly[0]); // sYCC is converted to RGB, so the samples change

            // sYCC first, sRGB after it: the first one counts
            Assert.Equal(syccOnly, Decode(WithColourBoxes(jp2, EnumeratedColour(18), EnumeratedColour(16))));

            // ROMM-RGB (21) is a colour space this reader does not convert, so the box that follows it is the first usable
            Assert.Equal(syccOnly, Decode(WithColourBoxes(jp2, EnumeratedColour(21), EnumeratedColour(18))));
        }

        /// <summary>
        /// The colour spaces the decoder does not convert gave no colour stage at all, and the image was then taken from before the
        /// palette and the channel definitions, which were lost with it.
        /// </summary>
        [Fact]
        public void ColourSpaceThatIsNotConverted_StillGetsItsChannelsInOrder()
        {
            var definitions = new CoreJ2K.j2k.fileformat.metadata.ChannelDefinitionData();
            definitions.AddColorChannel(0, 3);
            definitions.AddColorChannel(1, 2);
            definitions.AddColorChannel(2, 1);
            var (jp2, planes) = Encode(definitions);

            // CIELab (14) is a colour space that is not converted
            var decoded = Decode(WithColourBoxes(jp2, EnumeratedColour(14)));

            Assert.Equal(planes[2], decoded[0]);
            Assert.Equal(planes[1], decoded[1]);
            Assert.Equal(planes[0], decoded[2]);
        }
    }
}
