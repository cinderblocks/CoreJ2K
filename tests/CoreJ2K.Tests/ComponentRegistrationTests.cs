// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for Component Registration (CRG) marker support.
    /// Tests data structures, metadata API, and round-trip scenarios.
    /// </summary>
    public class ComponentRegistrationTests
    {
        [Fact]
        public void ComponentRegistrationData_Create_InitializesCorrectly()
        {
            var data = ComponentRegistrationData.Create(3);

            Assert.NotNull(data);
            Assert.Equal(3, data.NumComponents);
            Assert.Equal(3, data.HorizontalOffsets.Length);
            Assert.Equal(3, data.VerticalOffsets.Length);
            Assert.All(data.HorizontalOffsets, offset => Assert.Equal(0, offset));
            Assert.All(data.VerticalOffsets, offset => Assert.Equal(0, offset));
        }

        [Fact]
        public void ComponentRegistrationData_Create_WithOffsets()
        {
            var hOffsets = new[] { 100, 200, 300 };
            var vOffsets = new[] { 150, 250, 350 };

            var data = ComponentRegistrationData.Create(3, hOffsets, vOffsets);

            Assert.Equal(3, data.NumComponents);
            Assert.Equal(hOffsets, data.HorizontalOffsets);
            Assert.Equal(vOffsets, data.VerticalOffsets);
        }

        [Fact]
        public void ComponentRegistrationData_GetOffset_ReturnsCorrectValue()
        {
            var data = ComponentRegistrationData.Create(3, new[] { 100, 200, 300 }, new[] { 150, 250, 350 });

            Assert.Equal(100, data.GetHorizontalOffset(0));
            Assert.Equal(200, data.GetHorizontalOffset(1));
            Assert.Equal(300, data.GetHorizontalOffset(2));

            Assert.Equal(150, data.GetVerticalOffset(0));
            Assert.Equal(250, data.GetVerticalOffset(1));
            Assert.Equal(350, data.GetVerticalOffset(2));
        }

        [Fact]
        public void ComponentRegistrationData_SetOffset_UpdatesCorrectly()
        {
            var data = ComponentRegistrationData.Create(3);

            data.SetOffset(0, 100, 150);
            data.SetOffset(1, 200, 250);

            Assert.Equal(100, data.GetHorizontalOffset(0));
            Assert.Equal(150, data.GetVerticalOffset(0));
            Assert.Equal(200, data.GetHorizontalOffset(1));
            Assert.Equal(250, data.GetVerticalOffset(1));
        }

        [Fact]
        public void ComponentRegistrationData_FromFractionalPixels_ConvertsCorrectly()
        {
            // 0.5 pixels should be 32768 (half of 65536)
            var offset = ComponentRegistrationData.FromFractionalPixels(0.5);
            Assert.Equal(32768, offset);

            // 1.0 pixel should be 65536
            offset = ComponentRegistrationData.FromFractionalPixels(1.0);
            Assert.Equal(65536, offset);

            // 0.25 pixels should be 16384
            offset = ComponentRegistrationData.FromFractionalPixels(0.25);
            Assert.Equal(16384, offset);
        }

        [Fact]
        public void ComponentRegistrationData_ToFractionalPixels_ConvertsCorrectly()
        {
            // 32768 should be 0.5 pixels
            var pixels = ComponentRegistrationData.ToFractionalPixels(32768);
            Assert.Equal(0.5, pixels, 5);

            // 65536 should be 1.0 pixel
            pixels = ComponentRegistrationData.ToFractionalPixels(65536);
            Assert.Equal(1.0, pixels, 5);

            // 16384 should be 0.25 pixels
            pixels = ComponentRegistrationData.ToFractionalPixels(16384);
            Assert.Equal(0.25, pixels, 5);
        }

        [Fact]
        public void ComponentRegistrationData_CreateWithChromaPosition_CoSited()
        {
            // Co-sited: chroma samples at same location as luma (no offset)
            var data = ComponentRegistrationData.CreateWithChromaPosition(3, chromaPosition: 1);

            Assert.Equal(0, data.GetHorizontalOffset(0)); // Luma
            Assert.Equal(0, data.GetHorizontalOffset(1)); // Cb
            Assert.Equal(0, data.GetHorizontalOffset(2)); // Cr
            Assert.Equal(0, data.GetVerticalOffset(0));
            Assert.Equal(0, data.GetVerticalOffset(1));
            Assert.Equal(0, data.GetVerticalOffset(2));
        }

        [Fact]
        public void ComponentRegistrationData_CreateWithChromaPosition_Centered()
        {
            // Centered: chroma samples centered between luma samples (0.5 pixel offset)
            var data = ComponentRegistrationData.CreateWithChromaPosition(3, chromaPosition: 0);

            var expectedOffset = ComponentRegistrationData.FromFractionalPixels(0.5);

            Assert.Equal(0, data.GetHorizontalOffset(0)); // Luma - no offset
            Assert.Equal(expectedOffset, data.GetHorizontalOffset(1)); // Cb - offset
            Assert.Equal(expectedOffset, data.GetHorizontalOffset(2)); // Cr - offset
            Assert.Equal(0, data.GetVerticalOffset(0));
            Assert.Equal(expectedOffset, data.GetVerticalOffset(1));
            Assert.Equal(expectedOffset, data.GetVerticalOffset(2));
        }

        [Fact]
        public void ComponentRegistrationData_ToString_FormatsCorrectly()
        {
            var data = ComponentRegistrationData.Create(2);
            data.SetOffset(0, 32768, 16384); // 0.5, 0.25 pixels
            data.SetOffset(1, 65536, 49152); // 1.0, 0.75 pixels

            var str = data.ToString();

            Assert.Contains("2 components", str);
            Assert.Contains("Component 0", str);
            Assert.Contains("Component 1", str);
            Assert.Contains("0.5000", str);  // 0.5 pixels
            Assert.Contains("0.2500", str);  // 0.25 pixels
            Assert.Contains("1.0000", str);  // 1.0 pixel
            Assert.Contains("0.7500", str);  // 0.75 pixels
        }

        [Fact]
        public void J2KMetadata_SetComponentRegistration_CreatesData()
        {
            var metadata = new J2KMetadata();

            metadata.SetComponentRegistration(3, new[] { 100, 200, 300 }, new[] { 150, 250, 350 });

            Assert.NotNull(metadata.ComponentRegistration);
            Assert.Equal(3, metadata.ComponentRegistration.NumComponents);
            Assert.Equal(100, metadata.ComponentRegistration.GetHorizontalOffset(0));
            Assert.Equal(150, metadata.ComponentRegistration.GetVerticalOffset(0));
        }

        [Fact]
        public void J2KMetadata_SetChromaPosition_CreatesData()
        {
            var metadata = new J2KMetadata();

            metadata.SetChromaPosition(3, chromaPosition: 0);

            Assert.NotNull(metadata.ComponentRegistration);
            Assert.Equal(3, metadata.ComponentRegistration.NumComponents);

            var expectedOffset = ComponentRegistrationData.FromFractionalPixels(0.5);
            Assert.Equal(expectedOffset, metadata.ComponentRegistration.GetHorizontalOffset(1));
        }

        [Fact]
        public void ComponentRegistrationData_RoundTrip_FractionalPixels()
        {
            var originalPixels = 0.375; // 3/8 of a pixel

            var crgValue = ComponentRegistrationData.FromFractionalPixels(originalPixels);
            var roundTripPixels = ComponentRegistrationData.ToFractionalPixels(crgValue);

            Assert.Equal(originalPixels, roundTripPixels, 6);
        }

        [Fact]
        public void ComponentRegistrationData_InvalidComponentIndex_ReturnsZero()
        {
            var data = ComponentRegistrationData.Create(3);

            // Out of range index should return 0
            Assert.Equal(0, data.GetHorizontalOffset(-1));
            Assert.Equal(0, data.GetHorizontalOffset(10));
            Assert.Equal(0, data.GetVerticalOffset(-1));
            Assert.Equal(0, data.GetVerticalOffset(10));
        }

        [Fact]
        public void ComponentRegistrationData_SetOffset_InvalidIndex_ThrowsException()
        {
            var data = ComponentRegistrationData.Create(3);

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                data.SetOffset(-1, 100, 100));

            Assert.Throws<ArgumentOutOfRangeException>(() =>
                data.SetOffset(10, 100, 100));
        }

        [Fact]
        public void ComponentRegistrationData_MultipleComponents_IndependentOffsets()
        {
            var data = ComponentRegistrationData.Create(4);

            data.SetOffset(0, 1000, 2000);
            data.SetOffset(1, 3000, 4000);
            data.SetOffset(2, 5000, 6000);
            data.SetOffset(3, 7000, 8000);

            Assert.Equal(1000, data.GetHorizontalOffset(0));
            Assert.Equal(2000, data.GetVerticalOffset(0));
            Assert.Equal(3000, data.GetHorizontalOffset(1));
            Assert.Equal(4000, data.GetVerticalOffset(1));
            Assert.Equal(5000, data.GetHorizontalOffset(2));
            Assert.Equal(6000, data.GetVerticalOffset(2));
            Assert.Equal(7000, data.GetHorizontalOffset(3));
            Assert.Equal(8000, data.GetVerticalOffset(3));
        }

        [Fact]
        public void ComponentRegistrationData_UsageScenario_YCbCr420()
        {
            // Simulate YCbCr 4:2:0 with centered chroma
            var metadata = new J2KMetadata();

            // Set centered chroma positioning
            metadata.SetChromaPosition(numComponents: 3, chromaPosition: 0);

            var crg = metadata.ComponentRegistration;

            // Y component should have no offset
            Assert.Equal(0, crg.GetHorizontalOffset(0));
            Assert.Equal(0, crg.GetVerticalOffset(0));

            // Cb and Cr components should be centered (0.5 pixel offset)
            var centeredOffset = ComponentRegistrationData.FromFractionalPixels(0.5);
            Assert.Equal(centeredOffset, crg.GetHorizontalOffset(1));
            Assert.Equal(centeredOffset, crg.GetVerticalOffset(1));
            Assert.Equal(centeredOffset, crg.GetHorizontalOffset(2));
            Assert.Equal(centeredOffset, crg.GetVerticalOffset(2));
        }

        [Fact]
        public void ComponentRegistrationData_UsageScenario_CustomOffsets()
        {
            // Simulate custom component registration for specialized imaging
            var metadata = new J2KMetadata();

            // Set custom offsets (e.g., for Bayer pattern sensor alignment)
            var hOffsets = new int[4];
            var vOffsets = new int[4];

            // Component 0: no offset
            hOffsets[0] = 0;
            vOffsets[0] = 0;

            // Component 1: 0.5 pixel right
            hOffsets[1] = ComponentRegistrationData.FromFractionalPixels(0.5);
            vOffsets[1] = 0;

            // Component 2: 0.5 pixel down
            hOffsets[2] = 0;
            vOffsets[2] = ComponentRegistrationData.FromFractionalPixels(0.5);

            // Component 3: 0.5 pixel diagonally
            hOffsets[3] = ComponentRegistrationData.FromFractionalPixels(0.5);
            vOffsets[3] = ComponentRegistrationData.FromFractionalPixels(0.5);

            metadata.SetComponentRegistration(4, hOffsets, vOffsets);

            var crg = metadata.ComponentRegistration;
            Assert.Equal(4, crg.NumComponents);

            // Verify offsets
            Assert.Equal(0.0, ComponentRegistrationData.ToFractionalPixels(crg.GetHorizontalOffset(0)));
            Assert.Equal(0.5, ComponentRegistrationData.ToFractionalPixels(crg.GetHorizontalOffset(1)), 5);
            Assert.Equal(0.5, ComponentRegistrationData.ToFractionalPixels(crg.GetVerticalOffset(2)), 5);
            Assert.Equal(0.5, ComponentRegistrationData.ToFractionalPixels(crg.GetHorizontalOffset(3)), 5);
            Assert.Equal(0.5, ComponentRegistrationData.ToFractionalPixels(crg.GetVerticalOffset(3)), 5);
        }
    }
}
