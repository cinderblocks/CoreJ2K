// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Linq;
using CoreJ2K.j2k.roi.encoder;
using Xunit;

namespace CoreJ2K.Tests.ROI
{
    /// <summary>
    /// Tests for ROI mask caching functionality.
    /// </summary>
    public class ROIMaskCacheTests
    {
        [Fact]
        public void Constructor_CreatesEmptyCache()
        {
            var cache = new ROIMaskCache(10);
            
            Assert.Equal(10, cache.MaxCacheSize);
            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.Statistics.Hits);
            Assert.Equal(0, cache.Statistics.Misses);
        }
        
        [Fact]
        public void Constructor_InvalidSize_ThrowsException()
        {
            Assert.Throws<ArgumentException>(() => new ROIMaskCache(0));
            Assert.Throws<ArgumentException>(() => new ROIMaskCache(-1));
        }
        
        [Fact]
        public void AddMask_AddsSingleMask()
        {
            var cache = new ROIMaskCache(10);
            var key = new ROIMaskKey(0, 0, 0, 0, 64, 64, 12345);
            var mask = new int[64 * 64];
            
            cache.AddMask(key, mask);
            
            Assert.Equal(1, cache.Count);
            Assert.Equal(1, cache.Statistics.Adds);
        }
        
        [Fact]
        public void TryGetMask_ExistingKey_ReturnsTrue()
        {
            var cache = new ROIMaskCache(10);
            var key = new ROIMaskKey(0, 0, 0, 0, 64, 64, 12345);
            var originalMask = new int[64 * 64];
            for (int i = 0; i < originalMask.Length; i++)
                originalMask[i] = i;
            
            cache.AddMask(key, originalMask);
            var found = cache.TryGetMask(key, out var retrievedMask);
            
            Assert.True(found);
            Assert.NotNull(retrievedMask);
            Assert.Equal(originalMask.Length, retrievedMask.Length);
            Assert.Equal(originalMask, retrievedMask);
            Assert.Equal(1, cache.Statistics.Hits);
            Assert.Equal(0, cache.Statistics.Misses);
        }
        
        [Fact]
        public void TryGetMask_NonExistingKey_ReturnsFalse()
        {
            var cache = new ROIMaskCache(10);
            var key = new ROIMaskKey(0, 0, 0, 0, 64, 64, 12345);
            
            var found = cache.TryGetMask(key, out var mask);
            
            Assert.False(found);
            Assert.Null(mask);
            Assert.Equal(0, cache.Statistics.Hits);
            Assert.Equal(1, cache.Statistics.Misses);
        }
        
        [Fact]
        public void AddMask_MakesCopy_OriginalNotAffected()
        {
            var cache = new ROIMaskCache(10);
            var key = new ROIMaskKey(0, 0, 0, 0, 64, 64, 12345);
            var originalMask = new int[64 * 64];
            for (int i = 0; i < originalMask.Length; i++)
                originalMask[i] = i;
            
            cache.AddMask(key, originalMask);
            
            // Modify original
            originalMask[0] = 99999;
            
            cache.TryGetMask(key, out var retrievedMask);
            Assert.NotEqual(99999, retrievedMask[0]);
        }
        
        [Fact]
        public void AddMask_ExceedsCapacity_EvictsOldest()
        {
            var cache = new ROIMaskCache(3);
            var mask = new int[64];
            
            var key1 = new ROIMaskKey(0, 0, 0, 0, 8, 8, 1);
            var key2 = new ROIMaskKey(0, 0, 1, 0, 8, 8, 2);
            var key3 = new ROIMaskKey(0, 0, 2, 0, 8, 8, 3);
            var key4 = new ROIMaskKey(0, 0, 3, 0, 8, 8, 4);
            
            cache.AddMask(key1, mask);
            cache.AddMask(key2, mask);
            cache.AddMask(key3, mask);
            cache.AddMask(key4, mask); // Should evict key1
            
            Assert.Equal(3, cache.Count);
            Assert.False(cache.TryGetMask(key1, out _)); // key1 evicted
            Assert.True(cache.TryGetMask(key2, out _));
            Assert.True(cache.TryGetMask(key3, out _));
            Assert.True(cache.TryGetMask(key4, out _));
            Assert.Equal(1, cache.Statistics.Evictions);
        }
        
        [Fact]
        public void TryGetMask_UpdatesLRU()
        {
            var cache = new ROIMaskCache(2);
            var mask = new int[64];
            
            var key1 = new ROIMaskKey(0, 0, 0, 0, 8, 8, 1);
            var key2 = new ROIMaskKey(0, 0, 1, 0, 8, 8, 2);
            var key3 = new ROIMaskKey(0, 0, 2, 0, 8, 8, 3);
            
            cache.AddMask(key1, mask);
            cache.AddMask(key2, mask);
            
            // Access key1 to make it most recently used
            cache.TryGetMask(key1, out _);
            
            // Add key3, should evict key2 (least recently used)
            cache.AddMask(key3, mask);
            
            Assert.True(cache.TryGetMask(key1, out _)); // key1 still present
            Assert.False(cache.TryGetMask(key2, out _)); // key2 evicted
            Assert.True(cache.TryGetMask(key3, out _)); // key3 present
        }
        
        [Fact]
        public void Clear_RemovesAllMasks()
        {
            var cache = new ROIMaskCache(10);
            var mask = new int[64];
            
            for (int i = 0; i < 5; i++)
            {
                var key = new ROIMaskKey(0, 0, i, 0, 8, 8, i);
                cache.AddMask(key, mask);
            }
            
            Assert.Equal(5, cache.Count);
            
            cache.Clear();
            
            Assert.Equal(0, cache.Count);
            Assert.Equal(0, cache.Statistics.Hits);
            Assert.Equal(0, cache.Statistics.Misses);
            Assert.Equal(0, cache.Statistics.Adds);
        }
        
        [Fact]
        public void GetMemoryUsage_ReturnsApproximateSize()
        {
            var cache = new ROIMaskCache(10);
            var mask = new int[100]; // 400 bytes
            
            cache.AddMask(new ROIMaskKey(0, 0, 0, 0, 10, 10, 1), mask);
            cache.AddMask(new ROIMaskKey(0, 0, 1, 0, 10, 10, 2), mask);
            
            var usage = cache.GetMemoryUsage();
            
            Assert.True(usage >= 800); // At least 2 * 400 bytes
        }
        
        [Fact]
        public void Statistics_HitRatio_CalculatesCorrectly()
        {
            var cache = new ROIMaskCache(10);
            var key = new ROIMaskKey(0, 0, 0, 0, 8, 8, 1);
            var mask = new int[64];
            
            cache.AddMask(key, mask);
            
            // 3 hits, 2 misses
            cache.TryGetMask(key, out _); // hit
            cache.TryGetMask(key, out _); // hit
            cache.TryGetMask(key, out _); // hit
            cache.TryGetMask(new ROIMaskKey(0, 0, 1, 0, 8, 8, 2), out _); // miss
            cache.TryGetMask(new ROIMaskKey(0, 0, 2, 0, 8, 8, 3), out _); // miss
            
            Assert.Equal(3, cache.Statistics.Hits);
            Assert.Equal(2, cache.Statistics.Misses);
            Assert.Equal(5, cache.Statistics.TotalRequests);
            Assert.Equal(0.6, cache.Statistics.HitRatio, 2);
        }
        
        [Fact]
        public void Statistics_ToString_ReturnsFormattedString()
        {
            var cache = new ROIMaskCache(10);
            var stats = cache.Statistics;
            
            var str = stats.ToString();
            
            Assert.Contains("ROI Cache", str);
            Assert.Contains("Hits", str);
            Assert.Contains("Misses", str);
            Assert.Contains("Hit Ratio", str);
        }
        
        [Fact]
        public void ROIMaskKey_Equality_WorksCorrectly()
        {
            var key1 = new ROIMaskKey(0, 0, 10, 20, 64, 64, 12345);
            var key2 = new ROIMaskKey(0, 0, 10, 20, 64, 64, 12345);
            var key3 = new ROIMaskKey(0, 0, 10, 20, 64, 64, 99999);
            
            Assert.Equal(key1, key2);
            Assert.NotEqual(key1, key3);
            Assert.True(key1.Equals(key2));
            Assert.False(key1.Equals(key3));
        }
        
        [Fact]
        public void ROIMaskKey_GetHashCode_ConsistentForEqualKeys()
        {
            var key1 = new ROIMaskKey(0, 0, 10, 20, 64, 64, 12345);
            var key2 = new ROIMaskKey(0, 0, 10, 20, 64, 64, 12345);
            
            Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
        }
        
        [Fact]
        public void ROIMaskKey_ToString_ReturnsFormattedString()
        {
            var key = new ROIMaskKey(1, 2, 100, 200, 64, 64, 12345);
            var str = key.ToString();
            
            Assert.Contains("T1", str); // Tile
            Assert.Contains("C2", str); // Component
            Assert.Contains("100", str); // X
            Assert.Contains("200", str); // Y
            Assert.Contains("64", str); // Width/Height
        }
        
        [Fact]
        public void ConcurrentAccess_ThreadSafe()
        {
            var cache = new ROIMaskCache(100);
            var mask = new int[64];
            var exceptions = new System.Collections.Concurrent.ConcurrentBag<Exception>();
            
            var tasks = Enumerable.Range(0, 10).Select(i => 
                System.Threading.Tasks.Task.Run(() =>
                {
                    try
                    {
                        for (int j = 0; j < 20; j++)
                        {
                            var key = new ROIMaskKey(0, 0, j, 0, 8, 8, j);
                            cache.AddMask(key, mask);
                            cache.TryGetMask(key, out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        exceptions.Add(ex);
                    }
                })
            ).ToArray();
            
            System.Threading.Tasks.Task.WaitAll(tasks);
            
            Assert.Empty(exceptions);
            Assert.True(cache.Count <= 100);
        }
    }
}
