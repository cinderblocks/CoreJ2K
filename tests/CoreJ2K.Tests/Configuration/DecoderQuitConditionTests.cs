// Copyright (c) 2026 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.Configuration;
using CoreJ2K.Util;
using Xunit;

namespace CoreJ2K.Tests.Configuration
{
    /// <summary>A limit on code-blocks cannot be combined with parsing mode, which is on by default; setting a limit turns it off unless it was asked for.</summary>
    public class DecoderQuitConditionTests
    {
        private static byte[] Encoded()
        {
            var rnd = new Random(4);
            var comps = new int[3][];
            for (var c = 0; c < 3; c++)
            {
                comps[c] = new int[120 * 100];
                for (var i = 0; i < comps[c].Length; i++) comps[c][i] = rnd.Next(256) - 128;
            }
            var pl = new J2KEncoderConfiguration().WithBitrate(2f).ToParameterList();
            pl["threads"] = "1";
            return J2kImage.ToBytes(new InterleavedImageSource(120, 100, 3, 8, new bool[3], comps), null, pl)!;
        }

        [Fact]
        public void MaxCodeBlocks_WorksWithoutTouchingParsingMode()
        {
            var data = Encoded();
            var full = J2kImage.FromBytes(data, new J2KDecoderConfiguration());
            var limited = J2kImage.FromBytes(data, new J2KDecoderConfiguration().WithQuitConditions(q => q.WithMaxCodeBlocks(10)));

            Assert.NotEqual(full.GetComponent(0), limited.GetComponent(0));
        }

        [Fact]
        public void MaxCodeBlocks_WithParsingModeAskedFor_IsReportedByValidate()
        {
            var config = new J2KDecoderConfiguration().WithParsingMode(true).WithQuitConditions(q => q.WithMaxCodeBlocks(10));
            Assert.Contains(config.Validate(), e => e.Contains("parsing"));
        }

        [Fact]
        public void ParsingMode_StaysOnByDefault()
        {
            Assert.Equal("on", new J2KDecoderConfiguration().ToParameterList()["parsing"]);
            Assert.Equal("off", new J2KDecoderConfiguration().WithParsingMode(false).ToParameterList()["parsing"]);
            Assert.Equal("on", new J2KDecoderConfiguration().WithQuitConditions(q => q.WithMaxLayers(1)).ToParameterList()["parsing"]);
        }
    }
}
