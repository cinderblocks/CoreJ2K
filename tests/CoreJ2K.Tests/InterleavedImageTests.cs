using System;
using System.Linq;
using System.Reflection;
using Xunit;
using CoreJ2K.Util;

namespace CoreJ2K.Tests
{
    public class InterleavedImageTests
    {
        [Fact]
        public void ConstructorValidation_ThrowsOnInvalidArgs()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new InterleavedImage(0, 1, 1, new[] { 8 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InterleavedImage(1, 0, 1, new[] { 8 }));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InterleavedImage(1, 1, 0, new[] { 8 }));
            Assert.Throws<ArgumentNullException>(() => new InterleavedImage(1, 1, 1, null));
            Assert.Throws<ArgumentException>(() => new InterleavedImage(1, 1, 2, new[] { 8 }));

            // too large totalSamples -> ArgumentException
            Assert.Throws<ArgumentException>(() => new InterleavedImage(int.MaxValue, 2, 1, new[] { 8 }));
        }

        [Fact]
        public void ScalingCorrectness_ToBytesMatchesExpectedScaling()
        {
            var img = new InterleavedImage(3, 1, 1, new[] { 2 }); // bits=2 -> maxVal=3 -> scale=85
            // Set samples 0,1,3
            img.SetSample(0, 0, 0, 0);
            img.SetSample(1, 0, 0, 1);
            img.SetSample(2, 0, 0, 3);

            // Compute expected bytes using same scaling formula
            double scale = 255.0 / ((1UL << 2) - 1UL); // 255/3 = 85
            var expected = new byte[3];
            expected[0] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(scale * 0)));
            expected[1] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(scale * 1)));
            expected[2] = (byte)Math.Max(0, Math.Min(255, (int)Math.Round(scale * 3)));

            // Call private ToBytes via reflection
            var toBytes = typeof(InterleavedImage).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.ReturnType == typeof(byte[]) && m.Name == "ToBytes");

            var result = (byte[])toBytes.Invoke(null, new object[] { img.Width, img.Height, img.NumberOfComponents, new double[] { scale }, img.GetDataCopy() });

            Assert.Equal(expected, result);
        }

        [Fact]
        public void ToBytes_RoundingAndClamping_BehavesAsExpected()
        {
            var bits = 3; // maxVal = 7
            var img = new InterleavedImage(5, 1, 1, new[] { bits });

            var scale = 255.0 / ((1UL << bits) - 1UL);

            // Set a range of sample values including 0 and max
            for (int x = 0; x < 5; x++)
            {
                var sample = x; // 0..4
                img.SetSample(x, 0, 0, sample);
            }

            // Invoke private ToBytes to get bytes
            var toBytes = typeof(InterleavedImage).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .First(m => m.ReturnType == typeof(byte[]) && m.Name == "ToBytes");
            var result = (byte[])toBytes.Invoke(null, new object[] { img.Width, img.Height, img.NumberOfComponents, new double[] { scale }, img.GetDataCopy() });

            // Verify each output equals expected rounded/clamped value
            for (int x = 0; x < 5; x++)
            {
                var sample = x;
                var expected = (int)Math.Round(scale * sample);
                if (expected < 0) expected = 0;
                else if (expected > 255) expected = 255;
                Assert.Equal((byte)expected, result[x]);
            }
        }

        [Fact]
        public void CloneIndependence_CloneIsDeepCopy()
        {
            var img = new InterleavedImage(2, 1, 1, new[] { 8 });
            img.SetSample(0, 0, 0, 10);
            img.SetSample(1, 0, 0, 20);

            var clone = img.CloneInterleavedImage();

            // modify clone samples
            clone.SetSample(0, 0, 0, 99);

            // original should be unchanged
            Assert.Equal(10, img.GetSample(0, 0, 0));
            Assert.Equal(99, clone.GetSample(0, 0, 0));

            // GetDataCopy returns a copy
            var dataCopy = img.GetDataCopy();
            dataCopy[0] = 12345;
            Assert.NotEqual(12345, img.GetSample(0, 0, 0));
        }

        [Fact]
        public void SetComponent_ReplacesValues()
        {
            var img = new InterleavedImage(3, 1, 2, new[] { 8, 8 });
            // set initial other component values
            img.SetSample(0, 0, 0, 1);
            img.SetSample(1, 0, 0, 2);
            img.SetSample(2, 0, 0, 3);

            var samples = new[] { 10, 20, 30 };
            img.SetComponent(1, samples);

            // verify component 1 updated, component 0 unchanged
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(samples[x], img.GetSample(x, 0, 1));
                Assert.Equal(x + 1, img.GetSample(x, 0, 0));
            }
        }

        [Fact]
        public void SwapComponents_SwapsValues()
        {
            var img = new InterleavedImage(3, 1, 2, new[] { 8, 8 });
            // comp0 = [1,2,3], comp1=[10,20,30]
            img.SetComponent(0, new[] { 1, 2, 3 });
            img.SetComponent(1, new[] { 10, 20, 30 });

            img.SwapComponents(0, 1);

            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(10 * (x + 1) / (x + 1 == 1 ? 1 : 1), img.GetSample(x, 0, 0)); // equals 10,20,30
                Assert.Equal(1 + x, img.GetSample(x, 0, 1));
            }
        }

        [Fact]
        public void CopyComponent_CopiesValues()
        {
            var img = new InterleavedImage(3, 1, 2, new[] { 8, 8 });
            img.SetComponent(0, new[] { 5, 6, 7 });
            img.FillComponent(1, 0);

            img.CopyComponent(0, 1);

            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(img.GetSample(x, 0, 0), img.GetSample(x, 0, 1));
            }
        }

        [Fact]
        public void ApplyToComponent_TransformsValues()
        {
            var img = new InterleavedImage(4, 1, 1, new[] { 8 });
            img.SetComponent(0, new[] { 1, 2, 3, 4 });

            img.ApplyToComponent(0, v => v * 2);

            for (int x = 0; x < 4; x++)
            {
                Assert.Equal((x + 1) * 2, img.GetSample(x, 0, 0));
            }
        }

        [Fact]
        public void FillComponent_FillsValue()
        {
            var img = new InterleavedImage(3, 2, 1, new[] { 8 });
            img.FillComponent(0, 77);
            for (int y = 0; y < 2; y++)
            for (int x = 0; x < 3; x++)
            {
                Assert.Equal(77, img.GetSample(x, y, 0));
            }
        }

        [Fact]
        public void GetComponentBytes_ToComponentBytes_ProduceSame()
        {
            var img = new InterleavedImage(4, 1, 1, new[] { 8 });
            img.SetComponent(0, new[] { 0, 128, 255, 300 });

            var bytes = img.GetComponentBytes(0);
            var span = new byte[4];
            img.ToComponentBytes(0, span);

            Assert.Equal(bytes, span);
            // check clamping of 300 to 255
            Assert.Equal(255, bytes[3]);
        }

        [Fact]
        public void GetBitDepth_ReturnsCorrectValues()
        {
            var img = new InterleavedImage(10, 10, 3, new[] { 8, 10, 12 });
            
            Assert.Equal(8, img.GetBitDepth(0));
            Assert.Equal(10, img.GetBitDepth(1));
            Assert.Equal(12, img.GetBitDepth(2));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.GetBitDepth(-1));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.GetBitDepth(3));
        }

        [Fact]
        public void BitDepths_ReturnsReadOnlyList()
        {
            var img = new InterleavedImage(10, 10, 3, new[] { 8, 10, 12 });
            
            var bitDepths = img.BitDepths;
            Assert.Equal(3, bitDepths.Count);
            Assert.Equal(8, bitDepths[0]);
            Assert.Equal(10, bitDepths[1]);
            Assert.Equal(12, bitDepths[2]);
        }

        [Fact]
        public void GetPixel_ReturnsAllComponents()
        {
            var img = new InterleavedImage(3, 2, 3, new[] { 8, 8, 8 });
            img.SetSample(1, 1, 0, 100);
            img.SetSample(1, 1, 1, 150);
            img.SetSample(1, 1, 2, 200);

            var pixel = img.GetPixel(1, 1);
            
            Assert.Equal(3, pixel.Length);
            Assert.Equal(100, pixel[0]);
            Assert.Equal(150, pixel[1]);
            Assert.Equal(200, pixel[2]);
        }

        [Fact]
        public void GetPixel_WithSpan_FillsComponents()
        {
            var img = new InterleavedImage(3, 2, 3, new[] { 8, 8, 8 });
            img.SetSample(1, 1, 0, 100);
            img.SetSample(1, 1, 1, 150);
            img.SetSample(1, 1, 2, 200);

            Span<int> components = stackalloc int[3];
            img.GetPixel(1, 1, components);
            
            Assert.Equal(100, components[0]);
            Assert.Equal(150, components[1]);
            Assert.Equal(200, components[2]);
        }

        [Fact]
        public void SetPixel_SetsAllComponents()
        {
            var img = new InterleavedImage(3, 2, 3, new[] { 8, 8, 8 });
            
            ReadOnlySpan<int> components = stackalloc int[] { 100, 150, 200 };
            img.SetPixel(1, 1, components);
            
            Assert.Equal(100, img.GetSample(1, 1, 0));
            Assert.Equal(150, img.GetSample(1, 1, 1));
            Assert.Equal(200, img.GetSample(1, 1, 2));
        }

        [Fact]
        public void CopyRegion_CopiesDataCorrectly()
        {
            var src = new InterleavedImage(10, 10, 1, new[] { 8 });
            var dst = new InterleavedImage(10, 10, 1, new[] { 8 });
            
            // Fill source with pattern
            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 10; x++)
                    src.SetSample(x, y, 0, x * 10 + y);
            
            // Copy 3x3 region from (2,2) to (5,5)
            src.CopyRegion(2, 2, 3, 3, dst, 5, 5);
            
            // Verify copied region
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var expected = (x + 2) * 10 + (y + 2);
                    var actual = dst.GetSample(x + 5, y + 5, 0);
                    Assert.Equal(expected, actual);
                }
            }
            
            // Verify area outside copied region is still zero
            Assert.Equal(0, dst.GetSample(0, 0, 0));
            Assert.Equal(0, dst.GetSample(4, 5, 0));
        }

        [Fact]
        public void CopyRegion_ThrowsOnMismatchedComponents()
        {
            var src = new InterleavedImage(10, 10, 3, new[] { 8, 8, 8 });
            var dst = new InterleavedImage(10, 10, 1, new[] { 8 });
            
            Assert.Throws<ArgumentException>(() => 
                src.CopyRegion(0, 0, 5, 5, dst, 0, 0));
        }

        [Fact]
        public void CopyRegion_ThrowsOnInvalidBounds()
        {
            var src = new InterleavedImage(10, 10, 1, new[] { 8 });
            var dst = new InterleavedImage(10, 10, 1, new[] { 8 });
            
            // Source region out of bounds
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                src.CopyRegion(8, 8, 5, 5, dst, 0, 0));
            
            // Destination region out of bounds
            Assert.Throws<ArgumentOutOfRangeException>(() => 
                src.CopyRegion(0, 0, 5, 5, dst, 8, 8));
        }

        [Fact]
        public void Crop_CreatesNewImageWithRegion()
        {
            var src = new InterleavedImage(10, 10, 1, new[] { 8 });
            
            // Fill with pattern
            for (var y = 0; y < 10; y++)
                for (var x = 0; x < 10; x++)
                    src.SetSample(x, y, 0, x * 10 + y);
            
            // Crop 3x3 region starting at (2,2)
            var cropped = src.Crop(2, 2, 3, 3);
            
            Assert.Equal(3, cropped.Width);
            Assert.Equal(3, cropped.Height);
            Assert.Equal(1, cropped.NumberOfComponents);
            
            // Verify cropped data
            for (var y = 0; y < 3; y++)
            {
                for (var x = 0; x < 3; x++)
                {
                    var expected = (x + 2) * 10 + (y + 2);
                    var actual = cropped.GetSample(x, y, 0);
                    Assert.Equal(expected, actual);
                }
            }
        }

        [Fact]
        public void Crop_ThrowsOnInvalidBounds()
        {
            var img = new InterleavedImage(10, 10, 1, new[] { 8 });
            
            Assert.Throws<ArgumentOutOfRangeException>(() => img.Crop(-1, 0, 5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.Crop(0, -1, 5, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.Crop(0, 0, 0, 5));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.Crop(0, 0, 5, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => img.Crop(8, 8, 5, 5));
        }

        [Fact]
        public void Equals_ReturnsTrueForEqualImages()
        {
            var img1 = new InterleavedImage(3, 2, 1, new[] { 8 });
            var img2 = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            // Fill both with same data
            for (var i = 0; i < 6; i++)
            {
                img1.SetSample(i % 3, i / 3, 0, i);
                img2.SetSample(i % 3, i / 3, 0, i);
            }
            
            Assert.True(img1.Equals(img2));
            Assert.True(img1 == img2);
            Assert.False(img1 != img2);
        }

        [Fact]
        public void Equals_ReturnsFalseForDifferentImages()
        {
            var img1 = new InterleavedImage(3, 2, 1, new[] { 8 });
            var img2 = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            img1.SetSample(0, 0, 0, 100);
            img2.SetSample(0, 0, 0, 200);
            
            Assert.False(img1.Equals(img2));
            Assert.False(img1 == img2);
            Assert.True(img1 != img2);
        }

        [Fact]
        public void Equals_ReturnsFalseForDifferentDimensions()
        {
            var img1 = new InterleavedImage(3, 2, 1, new[] { 8 });
            var img2 = new InterleavedImage(2, 3, 1, new[] { 8 });
            
            Assert.False(img1.Equals(img2));
        }

        [Fact]
        public void Equals_ReturnsFalseForDifferentComponentCounts()
        {
            var img1 = new InterleavedImage(3, 2, 1, new[] { 8 });
            var img2 = new InterleavedImage(3, 2, 3, new[] { 8, 8, 8 });
            
            Assert.False(img1.Equals(img2));
        }

        [Fact]
        public void Equals_HandlesSameReference()
        {
            var img = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            Assert.True(img.Equals(img));
            Assert.True(img == img);
        }

        [Fact]
        public void Equals_HandlesNull()
        {
            var img = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            Assert.False(img.Equals(null));
            Assert.False(img == null);
            Assert.True(img != null);
        }

        [Fact]
        public void GetHashCode_IsConsistent()
        {
            var img = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            var hash1 = img.GetHashCode();
            var hash2 = img.GetHashCode();
            
            Assert.Equal(hash1, hash2);
        }

        [Fact]
        public void GetHashCode_SameForEqualDimensions()
        {
            var img1 = new InterleavedImage(3, 2, 1, new[] { 8 });
            var img2 = new InterleavedImage(3, 2, 1, new[] { 8 });
            
            // Note: Equal images should have equal hash codes
            // But data differences don't affect hash (by design - only dimensions)
            Assert.Equal(img1.GetHashCode(), img2.GetHashCode());
        }

        [Fact]
        public void ToString_ReturnsFormattedString()
        {
            var img = new InterleavedImage(640, 480, 3, new[] { 8, 8, 8 });
            
            var str = img.ToString();
            
            Assert.Contains("640", str);
            Assert.Contains("480", str);
            Assert.Contains("3", str);
            Assert.Contains("InterleavedImage", str);
        }
    }
}
