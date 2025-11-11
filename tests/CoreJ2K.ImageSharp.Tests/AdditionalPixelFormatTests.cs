using System;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K.j2k.image.input;

namespace CoreJ2K.ImageSharp.Tests
{
    public class AdditionalPixelFormatTests
    {
        [Fact]
        public void ImgReaderImageSharp_ReadsL8Correctly()
        {
            var img = new Image<L8>(1,1);
            img[0,0] = new L8(150);
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            var db = reader.GetInternCompData(blk, 0);
            var v = ((DataBlkInt)db).DataInt[0];
            Assert.Equal(150 - 128, v);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsL16Correctly()
        {
            var img = new Image<L16>(1,1);
            img[0,0] = new L16(0x1234);
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            var db = reader.GetInternCompData(blk, 0);
            var v = ((DataBlkInt)db).DataInt[0];
            // L16 is shifted down by >>8 in loader
            Assert.Equal((0x1234 >> 8) - 128, v);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsBgr24Correctly()
        {
            var img = new Image<Bgr24>(1,1);
            var px = img[0,0];
            px.R = 11; px.G = 22; px.B = 33;
            img[0,0] = px;
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            var r = ((DataBlkInt)reader.GetInternCompData(blk,0)).DataInt[0];
            var g = ((DataBlkInt)reader.GetInternCompData(blk,1)).DataInt[0];
            var b = ((DataBlkInt)reader.GetInternCompData(blk,2)).DataInt[0];
            Assert.Equal(11 - 128, r);
            Assert.Equal(22 - 128, g);
            Assert.Equal(33 - 128, b);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsBgra32Correctly()
        {
            var img = new Image<Bgra32>(1,1);
            var px = img[0,0];
            px.R = 45; px.G = 46; px.B = 47; px.A = 48;
            img[0,0] = px;
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            Assert.Equal(45 - 128, ((DataBlkInt)reader.GetInternCompData(blk,0)).DataInt[0]);
            Assert.Equal(46 - 128, ((DataBlkInt)reader.GetInternCompData(blk,1)).DataInt[0]);
            Assert.Equal(47 - 128, ((DataBlkInt)reader.GetInternCompData(blk,2)).DataInt[0]);
            Assert.Equal(48 - 128, ((DataBlkInt)reader.GetInternCompData(blk,3)).DataInt[0]);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsArgb32Correctly()
        {
            var img = new Image<Argb32>(1,1);
            var px = img[0,0];
            px.R = 70; px.G = 71; px.B = 72; px.A = 73;
            img[0,0] = px;
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            Assert.Equal(70 - 128, ((DataBlkInt)reader.GetInternCompData(blk,0)).DataInt[0]);
            Assert.Equal(71 - 128, ((DataBlkInt)reader.GetInternCompData(blk,1)).DataInt[0]);
            Assert.Equal(72 - 128, ((DataBlkInt)reader.GetInternCompData(blk,2)).DataInt[0]);
            Assert.Equal(73 - 128, ((DataBlkInt)reader.GetInternCompData(blk,3)).DataInt[0]);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsRgb48Correctly()
        {
            var img = new Image<Rgb48>(1,1);
            var px = img[0,0];
            px.R = 0x1234; px.G = 0x2345; px.B = 0x3456;
            img[0,0] = px;
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            Assert.Equal((0x1234 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,0)).DataInt[0]);
            Assert.Equal((0x2345 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,1)).DataInt[0]);
            Assert.Equal((0x3456 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,2)).DataInt[0]);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsRgba64Correctly()
        {
            var img = new Image<Rgba64>(1,1);
            var px = img[0,0];
            px.R = 0x1111; px.G = 0x2222; px.B = 0x3333; px.A = 0x4444;
            img[0,0] = px;
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            Assert.Equal((0x1111 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,0)).DataInt[0]);
            Assert.Equal((0x2222 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,1)).DataInt[0]);
            Assert.Equal((0x3333 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,2)).DataInt[0]);
            Assert.Equal((0x4444 >> 8) - 128, ((DataBlkInt)reader.GetInternCompData(blk,3)).DataInt[0]);
        }
    }
}
