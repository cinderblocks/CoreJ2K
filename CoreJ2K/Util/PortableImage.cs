// Copyright (c) 2007-2016 CSJ2K contributors.
// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;

namespace CoreJ2K.Util
{
    public sealed class PortableImage : IImage
    {
        #region FIELDS

        private readonly double[] byteScaling;

        #endregion

        #region CONSTRUCTORS

        internal PortableImage(int width, int height, int numberOfComponents, IEnumerable<int> bitsUsed)
        {
            Width = width;
            Height = height;
            NumberOfComponents = numberOfComponents;

            var bused = bitsUsed as int[] ?? bitsUsed.ToArray();
            byteScaling = new double[numberOfComponents];
            for (var i = 0; i < numberOfComponents; ++i)
            {
                byteScaling[i] = 255.0 / (1 << bused[i]);
            }

            Data = new int[numberOfComponents * width * height];
        }

        #endregion

        #region PROPERTIES

        public int Width { get; }

        public int Height { get; }

        public int NumberOfComponents { get; }

        internal int[] Data { get; }

        #endregion

        #region METHODS

        public T As<T>()
        {
            var image = ImageFactory.New<T>(Width, Height, NumberOfComponents,
                ToBytes(Width, Height, NumberOfComponents, byteScaling, Data));
            return image.As<T>();
        }

        public int[] GetComponent(int number)
        {
            if (number < 0 || number >= NumberOfComponents)
            {
                throw new ArgumentOutOfRangeException(nameof(number));
            }

            var length = Width * Height;
            var component = new int[length];

            // Copy interleaved samples for the requested component
            var srcIndex = number;
            for (var k = 0; k < length; ++k)
            {
                component[k] = Data[srcIndex];
                srcIndex += NumberOfComponents;
            }

            return component;
        }

        internal void FillRow(int rowIndex, int lineIndex, int rowWidth, int[] rowValues)
        {
            Array.Copy(
                rowValues,
                0,
                Data,
                NumberOfComponents * (rowIndex + lineIndex * rowWidth),
                rowValues.Length);
        }

        private static byte[] ToBytes(int width, int height, int numberOfComponents,
            IReadOnlyList<double> byteScaling, IReadOnlyList<int> data)
        {
            var pixels = width * height;
            var count = numberOfComponents * pixels;
            var bytes = new byte[count];

            // Convert interleaved int samples to bytes with per-component scaling and clamping
            for (var p = 0; p < pixels; ++p)
            {
                var baseIdx = p * numberOfComponents;
                for (var c = 0; c < numberOfComponents; ++c)
                {
                    // Scale and clamp to [0,255]
                    var scaled = byteScaling[c] * data[baseIdx + c];
                    var v = (int)scaled;
                    if (v < 0) v = 0;
                    else if (v > 255) v = 255;
                    bytes[baseIdx + c] = (byte)v;
                }
            }

            return bytes;
        }

        #endregion
    }
}
