// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.j2k.roi.encoder;
using Xunit;

namespace CoreJ2K.Tests.ROI
{
    /// <summary>
    /// Tests for optimized ROI mask representations (bit-packed and sparse).
    /// </summary>
    public class OptimizedROIMaskTests
    {
        #region BitPackedROIMask Tests
        
        [Fact]
        public void BitPackedROIMask_Constructor_CreatesEmptyMask()
        {
            var mask = new BitPackedROIMask(10, 20);
            
            Assert.Equal(10, mask.Width);
            Assert.Equal(20, mask.Height);
            Assert.Equal(200, mask.Length);
            Assert.False(mask.HasAnyROI());
        }
        
        [Fact]
        public void BitPackedROIMask_Constructor_InvalidDimensions_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => new BitPackedROIMask(0, 10));
            Assert.Throws<ArgumentException>(() => new BitPackedROIMask(10, 0));
            Assert.Throws<ArgumentException>(() => new BitPackedROIMask(-5, 10));
        }
        
        [Fact]
        public void BitPackedROIMask_FromIntArray_ConvertsCorrectly()
        {
            var intMask = new int[100];
            intMask[10] = 5;
            intMask[20] = 10;
            intMask[30] = 0;
            
            var mask = new BitPackedROIMask(intMask, 10, 10);
            
            Assert.True(mask[10]);
            Assert.True(mask[20]);
            Assert.False(mask[30]);
            Assert.Equal(2, mask.CountROICoefficients());
        }
        
        [Fact]
        public void BitPackedROIMask_FromIntArray_InvalidSize_ThrowsException()
        {
            var intMask = new int[100];
            
            Assert.Throws<ArgumentException>(() => new BitPackedROIMask(intMask, 10, 15));
        }
        
        [Fact]
        public void BitPackedROIMask_IndexerLinear_WorksCorrectly()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            mask[0] = true;
            mask[50] = true;
            mask[99] = true;
            
            Assert.True(mask[0]);
            Assert.True(mask[50]);
            Assert.True(mask[99]);
            Assert.False(mask[25]);
        }
        
        [Fact]
        public void BitPackedROIMask_IndexerCoordinate_WorksCorrectly()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            mask[0, 0] = true;
            mask[5, 5] = true;
            mask[9, 9] = true;
            
            Assert.True(mask[0, 0]);
            Assert.True(mask[5, 5]);
            Assert.True(mask[9, 9]);
            Assert.False(mask[3, 3]);
        }
        
        [Fact]
        public void BitPackedROIMask_ToIntArray_ConvertsBack()
        {
            var mask = new BitPackedROIMask(10, 10);
            mask[10] = true;
            mask[20] = true;
            mask[30] = true;
            
            var intArray = mask.ToIntArray(5);
            
            Assert.Equal(5, intArray[10]);
            Assert.Equal(5, intArray[20]);
            Assert.Equal(5, intArray[30]);
            Assert.Equal(0, intArray[0]);
            Assert.Equal(0, intArray[40]);
        }
        
        [Fact]
        public void BitPackedROIMask_SetAll_SetsAllBits()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            mask.SetAll(true);
            
            Assert.Equal(100, mask.CountROICoefficients());
            Assert.True(mask.HasAnyROI());
            
            mask.SetAll(false);
            
            Assert.Equal(0, mask.CountROICoefficients());
            Assert.False(mask.HasAnyROI());
        }
        
        [Fact]
        public void BitPackedROIMask_And_PerformsBitwiseAnd()
        {
            var mask1 = new BitPackedROIMask(10, 10);
            var mask2 = new BitPackedROIMask(10, 10);
            
            mask1[10] = true;
            mask1[20] = true;
            mask1[30] = true;
            
            mask2[20] = true;
            mask2[30] = true;
            mask2[40] = true;
            
            mask1.And(mask2);
            
            Assert.False(mask1[10]); // Only in mask1
            Assert.True(mask1[20]); // In both
            Assert.True(mask1[30]); // In both
            Assert.False(mask1[40]); // Only in mask2
        }
        
        [Fact]
        public void BitPackedROIMask_Or_PerformsBitwiseOr()
        {
            var mask1 = new BitPackedROIMask(10, 10);
            var mask2 = new BitPackedROIMask(10, 10);
            
            mask1[10] = true;
            mask1[20] = true;
            
            mask2[20] = true;
            mask2[30] = true;
            
            mask1.Or(mask2);
            
            Assert.True(mask1[10]); // Only in mask1
            Assert.True(mask1[20]); // In both
            Assert.True(mask1[30]); // Only in mask2
        }
        
        [Fact]
        public void BitPackedROIMask_Not_InvertsBits()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            mask[10] = true;
            mask[20] = true;
            
            mask.Not();
            
            Assert.False(mask[10]);
            Assert.False(mask[20]);
            Assert.True(mask[0]);
            Assert.True(mask[30]);
            Assert.Equal(98, mask.CountROICoefficients());
        }
        
        [Fact]
        public void BitPackedROIMask_CountROICoefficients_CountsCorrectly()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            for (int i = 0; i < 100; i += 10)
                mask[i] = true;
            
            Assert.Equal(10, mask.CountROICoefficients());
        }
        
        [Fact]
        public void BitPackedROIMask_HasAnyROI_DetectsROI()
        {
            var mask = new BitPackedROIMask(10, 10);
            
            Assert.False(mask.HasAnyROI());
            
            mask[50] = true;
            
            Assert.True(mask.HasAnyROI());
        }
        
        [Fact]
        public void BitPackedROIMask_Clone_CreatesIndependentCopy()
        {
            var mask1 = new BitPackedROIMask(10, 10);
            mask1[10] = true;
            mask1[20] = true;
            
            var mask2 = mask1.Clone();
            
            mask2[30] = true;
            
            Assert.True(mask1[10]);
            Assert.True(mask1[20]);
            Assert.False(mask1[30]); // mask1 unchanged
            
            Assert.True(mask2[10]);
            Assert.True(mask2[20]);
            Assert.True(mask2[30]);
        }
        
        [Fact]
        public void BitPackedROIMask_MemoryUsage_IsSmall()
        {
            var mask = new BitPackedROIMask(100, 100);
            var intArraySize = 100 * 100 * sizeof(int); // 40,000 bytes
            
            Assert.True(mask.MemoryUsage < intArraySize / 10); // Much less than int array
        }
        
        [Fact]
        public void BitPackedROIMask_GetMemorySavingsRatio_CalculatesCorrectly()
        {
            var mask = new BitPackedROIMask(100, 100);
            var ratio = mask.GetMemorySavingsRatio();
            
            Assert.True(ratio > 20); // At least 20x savings
        }
        
        #endregion
        
        #region SparseROIMask Tests
        
        [Fact]
        public void SparseROIMask_Constructor_CreatesFromDenseMask()
        {
            var denseMask = new int[100];
            denseMask[10] = 5;
            denseMask[20] = 10;
            denseMask[30] = 15;
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.Equal(10, sparse.Width);
            Assert.Equal(10, sparse.Height);
            Assert.Equal(3, sparse.ROICount);
        }
        
        [Fact]
        public void SparseROIMask_Constructor_InvalidSize_ThrowsException()
        {
            var denseMask = new int[100];
            
            Assert.Throws<ArgumentException>(() => new SparseROIMask(denseMask, 10, 15));
        }
        
        [Fact]
        public void SparseROIMask_GetValue_ReturnsCorrectValues()
        {
            var denseMask = new int[100];
            denseMask[10] = 5;
            denseMask[20] = 10;
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.Equal(5, sparse.GetValue(10));
            Assert.Equal(10, sparse.GetValue(20));
            Assert.Equal(0, sparse.GetValue(30));
        }
        
        [Fact]
        public void SparseROIMask_GetValueCoordinate_ReturnsCorrectValues()
        {
            var denseMask = new int[100];
            denseMask[23] = 7; // x=3, y=2 in 10x10 grid
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.Equal(7, sparse.GetValue(3, 2));
            Assert.Equal(0, sparse.GetValue(0, 0));
        }
        
        [Fact]
        public void SparseROIMask_IsROI_DetectsROICorrectly()
        {
            var denseMask = new int[100];
            denseMask[10] = 5;
            denseMask[20] = 10;
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.True(sparse.IsROI(10));
            Assert.True(sparse.IsROI(20));
            Assert.False(sparse.IsROI(30));
        }
        
        [Fact]
        public void SparseROIMask_IsROICoordinate_DetectsROICorrectly()
        {
            var denseMask = new int[100];
            denseMask[23] = 7; // x=3, y=2
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.True(sparse.IsROI(3, 2));
            Assert.False(sparse.IsROI(0, 0));
        }
        
        [Fact]
        public void SparseROIMask_ToDenseArray_ReconstructsOriginal()
        {
            var original = new int[100];
            original[10] = 5;
            original[20] = 10;
            original[30] = 15;
            
            var sparse = new SparseROIMask(original, 10, 10);
            var reconstructed = sparse.ToDenseArray();
            
            Assert.Equal(original, reconstructed);
        }
        
        [Fact]
        public void SparseROIMask_SparsityRatio_CalculatesCorrectly()
        {
            var denseMask = new int[100];
            for (int i = 0; i < 10; i++)
                denseMask[i] = i + 1;
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.Equal(0.1, sparse.SparsityRatio, 3); // 10/100 = 0.1
        }
        
        [Fact]
        public void SparseROIMask_IsMemoryEfficient_DetectsSparse()
        {
            // Very sparse mask (10%)
            var sparseMask = new int[1000];
            for (int i = 0; i < 100; i++)
                sparseMask[i] = i + 1;
            
            var sparse = new SparseROIMask(sparseMask, 100, 10);
            
            Assert.True(sparse.IsMemoryEfficient());
        }
        
        [Fact]
        public void SparseROIMask_IsMemoryEfficient_DetectsDense()
        {
            // Very dense mask (90%)
            var denseMask = new int[100];
            for (int i = 0; i < 90; i++)
                denseMask[i] = i + 1;
            
            var sparse = new SparseROIMask(denseMask, 10, 10);
            
            Assert.False(sparse.IsMemoryEfficient());
        }
        
        [Fact]
        public void SparseROIMask_GetMemorySavingsRatio_CalculatesForSparse()
        {
            var denseMask = new int[1000];
            for (int i = 0; i < 50; i++) // 5% filled
                denseMask[i] = i + 1;
            
            var sparse = new SparseROIMask(denseMask, 100, 10);
            var ratio = sparse.GetMemorySavingsRatio();
            
            Assert.True(ratio > 5); // Should have significant savings
        }
        
        [Fact]
        public void SparseROIMask_EmptyMask_HandledCorrectly()
        {
            var emptyMask = new int[100];
            
            var sparse = new SparseROIMask(emptyMask, 10, 10);
            
            Assert.Equal(0, sparse.ROICount);
            Assert.Equal(0.0, sparse.SparsityRatio);
            Assert.All(Enumerable.Range(0, 100), i => Assert.False(sparse.IsROI(i)));
        }
        
        [Fact]
        public void SparseROIMask_FullMask_HandledCorrectly()
        {
            var fullMask = new int[100];
            for (int i = 0; i < 100; i++)
                fullMask[i] = 1;
            
            var sparse = new SparseROIMask(fullMask, 10, 10);
            
            Assert.Equal(100, sparse.ROICount);
            Assert.Equal(1.0, sparse.SparsityRatio);
            Assert.All(Enumerable.Range(0, 100), i => Assert.True(sparse.IsROI(i)));
        }
        
        #endregion
        
        #region Comparison Tests
        
        [Fact]
        public void MemoryComparison_BitPacked_UsesMuchLessMemory()
        {
            var size = 1000;
            var intMask = new int[size];
            for (int i = 0; i < size / 2; i++)
                intMask[i] = 1;
            
            var bitPacked = new BitPackedROIMask(intMask, 100, 10);
            var intArraySize = size * sizeof(int);
            
            Assert.True(bitPacked.MemoryUsage < intArraySize / 20);
        }
        
        [Fact]
        public void MemoryComparison_Sparse_EfficientForSparseMasks()
        {
            var size = 10000;
            var intMask = new int[size];
            for (int i = 0; i < 100; i++) // 1% filled
                intMask[i] = 1;
            
            var sparse = new SparseROIMask(intMask, 100, 100);
            var intArraySize = size * sizeof(int);
            
            Assert.True(sparse.MemoryUsage < intArraySize / 10);
        }
        
        [Theory]
        [InlineData(64, 64, 10)] // 10% ROI
        [InlineData(128, 128, 25)] // 25% ROI
        [InlineData(256, 256, 50)] // 50% ROI
        public void RoundTrip_BitPacked_PreservesData(int width, int height, int roiPercent)
        {
            var size = width * height;
            var originalMask = new int[size];
            var roiCount = size * roiPercent / 100;
            
            for (int i = 0; i < roiCount; i++)
                originalMask[i] = 5;
            
            var bitPacked = new BitPackedROIMask(originalMask, width, height);
            var reconstructed = bitPacked.ToIntArray(5);
            
            Assert.Equal(originalMask, reconstructed);
        }
        
        [Theory]
        [InlineData(64, 64, 5)] // 5% ROI
        [InlineData(128, 128, 10)] // 10% ROI
        [InlineData(256, 256, 20)] // 20% ROI
        public void RoundTrip_Sparse_PreservesData(int width, int height, int roiPercent)
        {
            var size = width * height;
            var originalMask = new int[size];
            var roiCount = size * roiPercent / 100;
            
            for (int i = 0; i < roiCount; i++)
                originalMask[i] = i + 1;
            
            var sparse = new SparseROIMask(originalMask, width, height);
            var reconstructed = sparse.ToDenseArray();
            
            Assert.Equal(originalMask, reconstructed);
        }
        
        #endregion
    }
}
