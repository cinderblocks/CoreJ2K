// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Drawing;
using CoreJ2K.j2k.roi;
using Xunit;

namespace CoreJ2K.Tests.ROI
{
    /// <summary>
    /// Tests for the fluent ROI configuration API.
    /// </summary>
    public class ROIConfigurationTests
    {
        [Fact]
        public void Constructor_CreatesEmptyConfiguration()
        {
            var config = new ROIConfiguration();
            
            Assert.Empty(config.Regions);
            Assert.False(config.BlockAligned);
            Assert.Equal(-1, config.StartLevel);
            Assert.False(config.ForceGenericMaskGeneration);
            Assert.Equal(ROIScalingMode.MaxShift, config.ScalingMode);
        }
        
        [Fact]
        public void AddRectangle_WithCoordinates_AddsRegion()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 200, 300, 400);
            
            Assert.Single(config.Regions);
            var roi = config.Regions[0] as RectangularROI;
            Assert.NotNull(roi);
            Assert.Equal(0, roi.Component);
            Assert.Equal(100, roi.X);
            Assert.Equal(200, roi.Y);
            Assert.Equal(300, roi.Width);
            Assert.Equal(400, roi.Height);
            Assert.Equal(ROIShapeType.Rectangle, roi.ShapeType);
        }
        
        [Fact]
        public void AddRectangle_WithRectangleStruct_AddsRegion()
        {
            var rect = new Rectangle(50, 75, 200, 150);
            var config = new ROIConfiguration()
                .AddRectangle(1, rect);
            
            Assert.Single(config.Regions);
            var roi = config.Regions[0] as RectangularROI;
            Assert.NotNull(roi);
            Assert.Equal(1, roi.Component);
            Assert.Equal(50, roi.X);
            Assert.Equal(75, roi.Y);
            Assert.Equal(200, roi.Width);
            Assert.Equal(150, roi.Height);
        }
        
        [Fact]
        public void AddCircle_WithCoordinates_AddsRegion()
        {
            var config = new ROIConfiguration()
                .AddCircle(0, 500, 600, 150);
            
            Assert.Single(config.Regions);
            var roi = config.Regions[0] as CircularROI;
            Assert.NotNull(roi);
            Assert.Equal(0, roi.Component);
            Assert.Equal(500, roi.CenterX);
            Assert.Equal(600, roi.CenterY);
            Assert.Equal(150, roi.Radius);
            Assert.Equal(ROIShapeType.Circle, roi.ShapeType);
        }
        
        [Fact]
        public void AddCircle_WithPointStruct_AddsRegion()
        {
            var center = new Point(400, 300);
            var config = new ROIConfiguration()
                .AddCircle(2, center, 100);
            
            Assert.Single(config.Regions);
            var roi = config.Regions[0] as CircularROI;
            Assert.NotNull(roi);
            Assert.Equal(2, roi.Component);
            Assert.Equal(400, roi.CenterX);
            Assert.Equal(300, roi.CenterY);
            Assert.Equal(100, roi.Radius);
        }
        
        [Fact]
        public void AddArbitraryShape_WithValidPath_AddsRegion()
        {
            var tempFile = System.IO.Path.GetTempFileName();
            try
            {
                // Create a minimal PGM file
                System.IO.File.WriteAllText(tempFile, "P5\n2 2\n255\n    ");
                
                var config = new ROIConfiguration()
                    .AddArbitraryShape(0, tempFile);
                
                Assert.Single(config.Regions);
                var roi = config.Regions[0] as ArbitraryROI;
                Assert.NotNull(roi);
                Assert.Equal(0, roi.Component);
                Assert.Equal(tempFile, roi.MaskFilePath);
                Assert.Equal(ROIShapeType.Arbitrary, roi.ShapeType);
            }
            finally
            {
                if (System.IO.File.Exists(tempFile))
                    System.IO.File.Delete(tempFile);
            }
        }
        
        [Fact]
        public void AddArbitraryShape_WithNullPath_ThrowsException()
        {
            var config = new ROIConfiguration();
            
            Assert.Throws<ArgumentException>(() => config.AddArbitraryShape(0, null));
            Assert.Throws<ArgumentException>(() => config.AddArbitraryShape(0, ""));
            Assert.Throws<ArgumentException>(() => config.AddArbitraryShape(0, "   "));
        }
        
        [Fact]
        public void FluentAPI_ChainsMultipleCalls()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200)
                .AddCircle(0, 500, 500, 100)
                .SetBlockAlignment(true)
                .SetStartLevel(2)
                .SetScalingMode(ROIScalingMode.MaxShift)
                .ForceGenericMask(true);
            
            Assert.Equal(2, config.Regions.Count);
            Assert.True(config.BlockAligned);
            Assert.Equal(2, config.StartLevel);
            Assert.Equal(ROIScalingMode.MaxShift, config.ScalingMode);
            Assert.True(config.ForceGenericMaskGeneration);
        }
        
        [Fact]
        public void SetBlockAlignment_SetsProperty()
        {
            var config = new ROIConfiguration()
                .SetBlockAlignment(true);
            
            Assert.True(config.BlockAligned);
            
            config.SetBlockAlignment(false);
            Assert.False(config.BlockAligned);
        }
        
        [Fact]
        public void SetStartLevel_SetsProperty()
        {
            var config = new ROIConfiguration()
                .SetStartLevel(3);
            
            Assert.Equal(3, config.StartLevel);
        }
        
        [Fact]
        public void SetScalingMode_SetsProperty()
        {
            var config = new ROIConfiguration()
                .SetScalingMode(ROIScalingMode.MaxShift);
            
            Assert.Equal(ROIScalingMode.MaxShift, config.ScalingMode);
        }
        
        [Fact]
        public void ForceGenericMask_SetsProperty()
        {
            var config = new ROIConfiguration()
                .ForceGenericMask(true);
            
            Assert.True(config.ForceGenericMaskGeneration);
            
            config.ForceGenericMask(false);
            Assert.False(config.ForceGenericMaskGeneration);
        }
        
        [Fact]
        public void Validate_EmptyConfiguration_ReturnsError()
        {
            var config = new ROIConfiguration();
            var errors = config.Validate();
            
            Assert.Single(errors);
            Assert.Contains("at least one ROI", errors[0], StringComparison.OrdinalIgnoreCase);
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_ValidConfiguration_ReturnsNoErrors()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200);
            
            var errors = config.Validate();
            
            Assert.Empty(errors);
            Assert.True(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidStartLevel_ReturnsError()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200)
                .SetStartLevel(-5);
            
            var errors = config.Validate();
            
            Assert.Contains(errors, e => e.Contains("StartLevel"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidRectangle_ReturnsError()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, -10, 200);
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("width"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_InvalidCircle_ReturnsError()
        {
            var config = new ROIConfiguration()
                .AddCircle(0, 100, 100, -50);
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("radius"));
            Assert.False(config.IsValid);
        }
        
        [Fact]
        public void Validate_NonExistentMaskFile_ReturnsError()
        {
            var config = new ROIConfiguration()
                .AddArbitraryShape(0, "nonexistent_file.pgm");
            
            var errors = config.Validate();
            
            Assert.NotEmpty(errors);
            Assert.Contains(errors, e => e.Contains("not found"));
            Assert.False(config.IsValid);
        }
        
        [Theory]
        [InlineData(-1, -1, 100, 100)]
        [InlineData(100, -1, 100, 100)]
        [InlineData(-1, 100, 100, 100)]
        [InlineData(100, 100, 0, 100)]
        [InlineData(100, 100, 100, 0)]
        [InlineData(100, 100, -10, 100)]
        [InlineData(100, 100, 100, -10)]
        public void RectangularROI_InvalidParameters_FailsValidation(int x, int y, int width, int height)
        {
            var roi = new RectangularROI(0, x, y, width, height);
            var errors = roi.Validate();
            
            Assert.NotEmpty(errors);
        }
        
        [Theory]
        [InlineData(-1, 100, 50)]
        [InlineData(100, -1, 50)]
        [InlineData(100, 100, 0)]
        [InlineData(100, 100, -10)]
        public void CircularROI_InvalidParameters_FailsValidation(int cx, int cy, int radius)
        {
            var roi = new CircularROI(0, cx, cy, radius);
            var errors = roi.Validate();
            
            Assert.NotEmpty(errors);
        }
        
        [Fact]
        public void RectangularROI_ToString_ReturnsFormattedString()
        {
            var roi = new RectangularROI(1, 100, 200, 300, 400);
            var str = roi.ToString();
            
            Assert.Contains("Rectangle", str);
            Assert.Contains("Component=1", str);
            Assert.Contains("X=100", str);
            Assert.Contains("Y=200", str);
            Assert.Contains("Width=300", str);
            Assert.Contains("Height=400", str);
        }
        
        [Fact]
        public void CircularROI_ToString_ReturnsFormattedString()
        {
            var roi = new CircularROI(2, 500, 600, 150);
            var str = roi.ToString();
            
            Assert.Contains("Circular", str);
            Assert.Contains("Component=2", str);
            Assert.Contains("500", str);
            Assert.Contains("600", str);
            Assert.Contains("Radius=150", str);
        }
        
        [Fact]
        public void ArbitraryROI_ToString_ReturnsFormattedString()
        {
            var roi = new ArbitraryROI(0, "mask.pgm");
            var str = roi.ToString();
            
            Assert.Contains("Arbitrary", str);
            Assert.Contains("Component=0", str);
            Assert.Contains("mask.pgm", str);
        }
        
        [Fact]
        public void MultipleROIs_SameComponent_AllAdded()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200)
                .AddCircle(0, 500, 500, 100)
                .AddRectangle(0, 700, 700, 150, 150);
            
            Assert.Equal(3, config.Regions.Count);
            Assert.All(config.Regions, r => Assert.Equal(0, r.Component));
        }
        
        [Fact]
        public void MultipleROIs_DifferentComponents_AllAdded()
        {
            var config = new ROIConfiguration()
                .AddRectangle(0, 100, 100, 200, 200)
                .AddCircle(1, 500, 500, 100)
                .AddRectangle(2, 700, 700, 150, 150);
            
            Assert.Equal(3, config.Regions.Count);
            Assert.Equal(0, config.Regions[0].Component);
            Assert.Equal(1, config.Regions[1].Component);
            Assert.Equal(2, config.Regions[2].Component);
        }
    }
}
