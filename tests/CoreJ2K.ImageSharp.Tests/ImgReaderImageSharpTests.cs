using System;
using Xunit;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using CoreJ2K.j2k.image.input;

namespace CoreJ2K.ImageSharp.Tests
{
    public class ImgReaderImageSharpTests
    {
        [Fact]
        public void ImgReaderImageSharp_ReadsRgb24Correctly()
        {
            var img = new Image<Rgb24>(2,2);
            // pixel layout: top-left (0,0), then (1,0), (0,1), (1,1)
            img[0,0] = new Rgb24(10,20,30);
            img[1,0] = new Rgb24(40,50,60);
            img[0,1] = new Rgb24(70,80,90);
            img[1,1] = new Rgb24(100,110,120);

            using var reader = new ImgReaderImageSharp(img);

            var blk = new DataBlkInt(0,0,2,2);
            var db = reader.GetInternCompData(blk, 0); // red component
            var data = ((DataBlkInt)db).DataInt;

            Assert.Equal(4, data.Length);
            Assert.Equal(10 - 128, data[0]);
            Assert.Equal(40 - 128, data[1]);
            Assert.Equal(70 - 128, data[2]);
            Assert.Equal(100 - 128, data[3]);
        }

        [Fact]
        public void ImgReaderImageSharp_ReadsRgba32AlphaCorrectly()
        {
            var img = new Image<Rgba32>(1,1);
            img[0,0] = new Rgba32(200,201,202,203);
            using var reader = new ImgReaderImageSharp(img);
            var blk = new DataBlkInt(0,0,1,1);
            var alpha = reader.GetInternCompData(blk, 3);
            var a = ((DataBlkInt)alpha).DataInt[0];
            Assert.Equal(203 - 128, a);
        }
    }
}
