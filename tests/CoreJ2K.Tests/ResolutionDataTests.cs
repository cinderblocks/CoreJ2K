// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using Xunit;
using CoreJ2K.j2k.fileformat.metadata;

namespace CoreJ2K.Tests
{
    /// <summary>
    /// Tests for resolution metadata support in JPEG2000 files.
    /// </summary>
    public class ResolutionDataTests
    {
        [Fact]
        public void ResolutionData_DefaultConstructor_HasNoResolution()
        {
            var resolution = new ResolutionData();

            Assert.False(resolution.HasResolution);
            Assert.False(resolution.HasCaptureResolution);
            Assert.False(resolution.HasDisplayResolution);
        }

        [Fact]
        public void ResolutionData_SetCaptureDpi_StoresValues()
        {
            var resolution = new ResolutionData();
            
            resolution.SetCaptureDpi(300.0, 300.0);

            Assert.True(resolution.HasCaptureResolution);
            Assert.Equal(300.0, resolution.HorizontalCaptureDpi.Value, 2);
            Assert.Equal(300.0, resolution.VerticalCaptureDpi.Value, 2);
        }

        [Fact]
        public void ResolutionData_SetDisplayDpi_StoresValues()
        {
            var resolution = new ResolutionData();
            
            resolution.SetDisplayDpi(96.0, 96.0);

            Assert.True(resolution.HasDisplayResolution);
            Assert.Equal(96.0, resolution.HorizontalDisplayDpi.Value, 2);
            Assert.Equal(96.0, resolution.VerticalDisplayDpi.Value, 2);
        }

        [Fact]
        public void ResolutionData_SetCaptureResolution_ConvertsToPixelsPerMeter()
        {
            var resolution = new ResolutionData();
            
            // Set 300 DPI (should be ~11811 pixels per meter)
            resolution.SetCaptureDpi(300.0, 300.0);

            Assert.NotNull(resolution.HorizontalCaptureResolution);
            Assert.NotNull(resolution.VerticalCaptureResolution);
            
            // Verify conversion (300 DPI * 39.3701 inches/meter = ~11811 pixels/meter)
            Assert.Equal(11811.03, resolution.HorizontalCaptureResolution.Value, 0.5);
            Assert.Equal(11811.03, resolution.VerticalCaptureResolution.Value, 0.5);
        }

        [Fact]
        public void ResolutionData_SetCaptureResolution_DirectPixelsPerMeter()
        {
            var resolution = new ResolutionData();
            
            resolution.SetCaptureResolution(11811.0, 11811.0);

            Assert.True(resolution.HasCaptureResolution);
            Assert.Equal(11811.0, resolution.HorizontalCaptureResolution.Value);
            Assert.Equal(11811.0, resolution.VerticalCaptureResolution.Value);
            
            // Should convert back to ~300 DPI
            Assert.Equal(300.0, resolution.HorizontalCaptureDpi.Value, 0.5);
            Assert.Equal(300.0, resolution.VerticalCaptureDpi.Value, 0.5);
        }

        [Fact]
        public void ResolutionData_BothCaptureAndDisplay_BothPresent()
        {
            var resolution = new ResolutionData();
            
            resolution.SetCaptureDpi(300.0, 300.0);
            resolution.SetDisplayDpi(96.0, 96.0);

            Assert.True(resolution.HasResolution);
            Assert.True(resolution.HasCaptureResolution);
            Assert.True(resolution.HasDisplayResolution);
            
            Assert.Equal(300.0, resolution.HorizontalCaptureDpi.Value, 2);
            Assert.Equal(96.0, resolution.HorizontalDisplayDpi.Value, 2);
        }

        [Fact]
        public void ResolutionData_ToString_FormatsCorrectly()
        {
            var resolution = new ResolutionData();
            resolution.SetCaptureDpi(300.0, 300.0);

            var str = resolution.ToString();

            Assert.Contains("300", str);
            Assert.Contains("DPI", str);
            Assert.Contains("Capture", str);
        }

        [Fact]
        public void ResolutionData_ToString_NoResolution()
        {
            var resolution = new ResolutionData();

            var str = resolution.ToString();

            Assert.Contains("No resolution", str);
        }

        [Fact]
        public void ResolutionData_CommonDpiConstants_AreCorrect()
        {
            Assert.Equal(72.0, ResolutionData.CommonDpi.Screen72);
            Assert.Equal(96.0, ResolutionData.CommonDpi.Screen96);
            Assert.Equal(150.0, ResolutionData.CommonDpi.Print150);
            Assert.Equal(300.0, ResolutionData.CommonDpi.Print300);
            Assert.Equal(600.0, ResolutionData.CommonDpi.Print600);
            Assert.Equal(1200.0, ResolutionData.CommonDpi.Print1200);
        }

        [Fact]
        public void ResolutionData_Screen96Dpi_ConvertsCorrectly()
        {
            var resolution = new ResolutionData();
            resolution.SetDisplayDpi(ResolutionData.CommonDpi.Screen96, ResolutionData.CommonDpi.Screen96);

            Assert.Equal(96.0, resolution.HorizontalDisplayDpi.Value, 2);
            Assert.Equal(96.0, resolution.VerticalDisplayDpi.Value, 2);
            
            // 96 DPI = 96 * 39.3701 = ~3779.53 pixels per meter
            Assert.Equal(3779.53, resolution.HorizontalDisplayResolution.Value, 0.5);
        }

        [Fact]
        public void ResolutionData_Print300Dpi_ConvertsCorrectly()
        {
            var resolution = new ResolutionData();
            resolution.SetCaptureDpi(ResolutionData.CommonDpi.Print300, ResolutionData.CommonDpi.Print300);

            Assert.Equal(300.0, resolution.HorizontalCaptureDpi.Value, 2);
            Assert.Equal(11811.03, resolution.HorizontalCaptureResolution.Value, 0.5);
        }

        [Fact]
        public void ResolutionData_AsymmetricResolution_HandlesCorrectly()
        {
            var resolution = new ResolutionData();
            resolution.SetCaptureDpi(300.0, 600.0);

            Assert.Equal(300.0, resolution.HorizontalCaptureDpi.Value, 2);
            Assert.Equal(600.0, resolution.VerticalCaptureDpi.Value, 2);
        }

        [Fact]
        public void J2KMetadata_SetResolutionDpi_CreatesResolutionData()
        {
            var metadata = new J2KMetadata();
            
            metadata.SetResolutionDpi(300.0, 300.0, isCapture: true);

            Assert.NotNull(metadata.Resolution);
            Assert.True(metadata.Resolution.HasCaptureResolution);
            Assert.Equal(300.0, metadata.Resolution.HorizontalCaptureDpi.Value, 2);
        }

        [Fact]
        public void J2KMetadata_SetResolutionDpi_DisplayResolution()
        {
            var metadata = new J2KMetadata();
            
            metadata.SetResolutionDpi(96.0, 96.0, isCapture: false);

            Assert.NotNull(metadata.Resolution);
            Assert.True(metadata.Resolution.HasDisplayResolution);
            Assert.Equal(96.0, metadata.Resolution.HorizontalDisplayDpi.Value, 2);
        }

        [Fact]
        public void J2KMetadata_SetBothResolutions_BothStored()
        {
            var metadata = new J2KMetadata();
            
            metadata.SetResolutionDpi(300.0, 300.0, isCapture: true);
            metadata.SetResolutionDpi(96.0, 96.0, isCapture: false);

            Assert.NotNull(metadata.Resolution);
            Assert.True(metadata.Resolution.HasCaptureResolution);
            Assert.True(metadata.Resolution.HasDisplayResolution);
            Assert.Equal(300.0, metadata.Resolution.HorizontalCaptureDpi.Value, 2);
            Assert.Equal(96.0, metadata.Resolution.HorizontalDisplayDpi.Value, 2);
        }

        [Fact]
        public void ResolutionData_HighResolution_HandlesLargeValues()
        {
            var resolution = new ResolutionData();
            resolution.SetCaptureDpi(2400.0, 2400.0);

            Assert.Equal(2400.0, resolution.HorizontalCaptureDpi.Value, 2);
            Assert.NotNull(resolution.HorizontalCaptureResolution);
            
            // 2400 DPI should be ~94488 pixels per meter
            Assert.True(resolution.HorizontalCaptureResolution.Value > 90000);
        }

        [Fact]
        public void ResolutionData_LowResolution_HandlesSmallValues()
        {
            var resolution = new ResolutionData();
            resolution.SetDisplayDpi(72.0, 72.0);

            Assert.Equal(72.0, resolution.HorizontalDisplayDpi.Value, 2);
            
            // 72 DPI should be ~2834.65 pixels per meter
            Assert.Equal(2834.65, resolution.HorizontalDisplayResolution.Value, 0.5);
        }

        [Fact]
        public void ResolutionData_RoundTripConversion_MaintainsPrecision()
        {
            var resolution = new ResolutionData();
            var originalDpi = 300.0;
            
            resolution.SetCaptureDpi(originalDpi, originalDpi);
            var convertedDpi = resolution.HorizontalCaptureDpi.Value;

            // Should round-trip with minimal loss
            Assert.Equal(originalDpi, convertedDpi, 0.01);
        }
    }
}
