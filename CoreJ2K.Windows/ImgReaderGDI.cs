/*
*
* Class:                   ImgReaderGDI
*
* Description:             Image writer for System.Drawing.GDI streams
*
* */

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace CoreJ2K.j2k.image.input
{
    public class ImgReaderGDI : ImgReader
    {
        /// <summary>The number of bits that determine the nominal dynamic range </summary>
        private readonly int rb;

        /// <summary>Buffer for the components of each pixel(in the current block) </summary>
        private int[][] barr;

        /// <summary>Data block used only to store coordinates of the buffered blocks </summary>
        private readonly DataBlkInt dbi = new DataBlkInt();

        /// <summary>Temporary DataBlkInt object (needed when encoder uses floating-point
        /// filters). This avoids allocating new DataBlk at each time 
        /// </summary>
        private DataBlkInt intBlk;

        private Image image;

        public ImgReaderGDI(Image image)
        {
            if (image.PixelFormat != PixelFormat.Format24bppRgb && image.PixelFormat != PixelFormat.Format32bppArgb)
                throw new ArgumentException("Only 24bpp RGB and 32bpp ARGB images are currently supported");

            this.image = image;

            w = image.Width;
            h = image.Height;

            nc = (image.PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3;
            rb = 8;
        }

        /// <summary> Closes the underlying file from where the image data is being read. No
        /// operations are possible after a call to this method.
        /// 
        /// </summary>
        /// <exception cref="IOException">If an I/O error occurs.
        /// 
        /// </exception>
        public override void Close()
        {
            image.Dispose();
            image = null;

            barr = null;
        }

        /// <summary> Returns the number of bits corresponding to the nominal range of the
        /// data in the specified component. This is the value rb (range bits) that
        /// was specified in the constructor, which normally is 8 for non bilevel
        /// data, and 1 for bilevel data.
        /// 
        /// If this number is b then the nominal range is between
        /// -2^(b-1) and 2^(b-1)-1, since unsigned data is level shifted to have a
        /// nominal average of 0.
        /// 
        /// </summary>
        /// <param name="compIndex">The index of the component.
        /// 
        /// </param>
        /// <returns> The number of bits corresponding to the nominal range of the
        /// data. For floating-point data this value is not applicable and the
        /// return value is undefined.
        /// 
        /// </returns>
        public override int getNomRangeBits(int compIndex)
        {
            // Check component index
            if (compIndex < 0 || compIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");

            return rb;
        }

        /// <summary> Returns the position of the fixed point in the specified component
        /// (i.e. the number of fractional bits), which is always 0 for this
        /// ImgReader.
        /// 
        /// </summary>
        /// <param name="compIndex">The index of the component.
        /// 
        /// </param>
        /// <returns> The position of the fixed-point (i.e. the number of fractional
        /// bits). Always 0 for this ImgReader.
        /// 
        /// </returns>
        public override int GetFixedPoint(int compIndex)
        {
            // Check component index
            if (compIndex < 0 || compIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");
            return 0;
        }

        /// <summary> Returns, in the blk argument, the block of image data containing the
        /// specified rectangular area, in the specified component. The data is
        /// returned, as a reference to the internal data, if any, instead of as a
        /// copy, therefore the returned data should not be modified.
        /// 
        /// After being read the coefficients are level shifted by subtracting
        /// 2^(nominal bit range - 1)
        /// 
        /// The rectangular area to return is specified by the 'ulx', 'uly', 'w'
        /// and 'h' members of the 'blk' argument, relative to the current
        /// tile. These members are not modified by this method. The 'offset' and
        /// 'scanw' of the returned data can be arbitrary. See the 'DataBlk' class.
        /// 
        /// If the data array in <tt>blk</tt> is <tt>null</tt>, then a new one
        /// is created if necessary. The implementation of this interface may
        /// choose to return the same array or a new one, depending on what is more
        /// efficient. Therefore, the data array in <tt>blk</tt> prior to the
        /// method call should not be considered to contain the returned data, a
        /// new array may have been created. Instead, get the array from
        /// <tt>blk</tt> after the method has returned.
        /// 
        /// The returned data always has its 'progressive' attribute unset
        /// (i.e. false).
        /// 
        /// When an I/O exception is encountered the JJ2KExceptionHandler is
        /// used. The exception is passed to its handleException method. The action
        /// that is taken depends on the action that has been registered in
        /// JJ2KExceptionHandler. See JJ2KExceptionHandler for details.
        /// 
        /// This method implements buffering for the 3 components: When the
        /// first one is asked, all the 3 components are read and stored until they
        /// are needed.
        /// 
        /// </summary>
        /// <param name="blk">Its coordinates and dimensions specify the area to
        /// return. Some fields in this object are modified to return the data.
        /// 
        /// </param>
        /// <param name="compIndex">The index of the component from which to get the data. Only 0,
        /// 1 and 3 are valid.
        /// 
        /// </param>
        /// <returns> The requested DataBlk
        /// 
        /// </returns>
        /// <seealso cref="GetCompData">
        /// 
        /// </seealso>
        /// <seealso cref="JJ2KExceptionHandler">
        /// 
        /// </seealso>
        public override DataBlk GetInternCompData(DataBlk blk, int compIndex)
        {
            // Check component index
            if (compIndex < 0 || compIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");

            // Check type of block provided as an argument
            if (blk.DataType != DataBlk.TYPE_INT)
            {
                if (intBlk == null)
                    intBlk = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
                else
                {
                    intBlk.ulx = blk.ulx;
                    intBlk.uly = blk.uly;
                    intBlk.w = blk.w;
                    intBlk.h = blk.h;
                }
                blk = intBlk;
            }

            // If asking a component for the first time for this block, read all of the components
            if ((barr == null) || (dbi.ulx > blk.ulx) || (dbi.uly > blk.uly) || (dbi.ulx + dbi.w < blk.ulx + blk.w) || (dbi.uly + dbi.h < blk.uly + blk.h))
            {
                var componentCount = (image.PixelFormat == PixelFormat.Format32bppArgb) ? 4 : 3;

                barr = new int[componentCount][];

                // Reset data arrays if needed
                if (barr[compIndex] == null || barr[compIndex].Length < blk.w * blk.h)
                {
                    barr[compIndex] = new int[blk.w * blk.h];
                }
                blk.Data = barr[compIndex];

                var i = (compIndex + 1) % 3;
                if (barr[i] == null || barr[i].Length < blk.w * blk.h)
                {
                    barr[i] = new int[blk.w * blk.h];
                }
                i = (compIndex + 2) % 3;
                if (barr[i] == null || barr[i].Length < blk.w * blk.h)
                {
                    barr[i] = new int[blk.w * blk.h];
                }

                if (componentCount == 4)
                {
                    if (barr[3] == null || barr[3].Length < blk.w * blk.h)
                        barr[3] = new int[blk.w * blk.h];
                }

                // set attributes of the DataBlk used for buffering
                dbi.ulx = blk.ulx;
                dbi.uly = blk.uly;
                dbi.w = blk.w;
                dbi.h = blk.h;
                dbi.scanw = blk.w;

                var red = barr[0];
                var green = barr[1];
                var blue = barr[2];
                var alpha = (componentCount > 3) ? barr[3] : null;

                var bitmap = (Bitmap)image;
                var pixelFormat = (componentCount == 3) ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
                var data = bitmap.LockBits(new Rectangle(blk.ulx, blk.uly, blk.w, blk.h), ImageLockMode.ReadOnly, pixelFormat);
                try
                {
                    var stride = data.Stride;
                    var absStride = Math.Abs(stride);
                    var totalBytes = absStride * blk.h;
                    var raw = new byte[totalBytes];
                    Marshal.Copy(data.Scan0, raw, 0, totalBytes);

                    var bytesPerPixel = (componentCount == 3) ? 3 : 4;
                    var k = 0;
                    for (var row = 0; row < blk.h; row++)
                    {
                        var rowStart = row * absStride;
                        for (var col = 0; col < blk.w; col++)
                        {
                            var idx = rowStart + col * bytesPerPixel;
                            var b = raw[idx + 0];
                            var g = raw[idx + 1];
                            var r = raw[idx + 2];
                            blue[k] = (b & 0xFF) - 128;
                            green[k] = (g & 0xFF) - 128;
                            red[k] = (r & 0xFF) - 128;
                            if (alpha != null)
                            {
                                var a = raw[idx + 3];
                                alpha[k] = (a & 0xFF) - 128;
                            }
                            ++k;
                        }
                    }
                }
                finally
                {
                    bitmap.UnlockBits(data);
                }

                barr[0] = red;
                barr[1] = green;
                barr[2] = blue;
                if (alpha != null) barr[3] = alpha;

                // Set buffer attributes
                blk.Data = barr[compIndex];
                blk.offset = 0;
                blk.scanw = blk.w;
            }
            else
            {
                //Asking for the 2nd or 3rd (or 4th) block component
                blk.Data = barr[compIndex];
                blk.offset = (blk.ulx - dbi.ulx) + (blk.uly - dbi.uly) * dbi.scanw;
                blk.scanw = dbi.scanw;
            }

            // Turn off the progressive attribute
            blk.progressive = false;
            return blk;
        }

        /// <summary> Returns, in the blk argument, a block of image data containing the
        /// specified rectangular area, in the specified component. The data is
        /// returned, as a copy of the internal data, therefore the returned data
        /// can be modified "in place".
        /// 
        /// After being read the coefficients are level shifted by subtracting
        /// 2^(nominal bit range - 1)
        /// 
        /// The rectangular area to return is specified by the 'ulx', 'uly', 'w'
        /// and 'h' members of the 'blk' argument, relative to the current
        /// tile. These members are not modified by this method. The 'offset' of
        /// the returned data is 0, and the 'scanw' is the same as the block's
        /// width. See the 'DataBlk' class.
        /// 
        /// If the data array in 'blk' is 'null', then a new one is created. If
        /// the data array is not 'null' then it is reused, and it must be large
        /// enough to contain the block's data. Otherwise, an 'ArrayStoreException'
        /// or an 'IndexOutOfBoundsException' is thrown by the Java system.
        /// 
        /// The returned data has its 'progressive' attribute unset
        /// (i.e. false).
        /// 
        /// When an I/O exception is encountered the JJ2KExceptionHandler is
        /// used. The exception is passed to its handleException method. The action
        /// that is taken depends on the action that has been registered in
        /// JJ2KExceptionHandler. See JJ2KExceptionHandler for details.
        /// 
        /// </summary>
        /// <param name="blk">Its coordinates and dimensions specify the area to
        /// return. If it contains a non-null data array, then it must have the
        /// correct dimensions. If it contains a null data array a new one is
        /// created. The fields in this object are modified to return the data.
        /// 
        /// </param>
        /// <param name="c">The index of the component from which to get the data. Only
        /// 0,1 and 2 are valid.
        /// 
        /// </param>
        /// <returns> The requested DataBlk
        /// 
        /// </returns>
        /// <seealso cref="GetInternCompData">
        /// 
        /// </seealso>
        /// <seealso cref="JJ2KExceptionHandler">
        /// 
        /// </seealso>
        public override DataBlk GetCompData(DataBlk blk, int c)
        {
            // Check type of block provided as an argument
            if (blk.DataType != DataBlk.TYPE_INT)
            {
                var tmp = new DataBlkInt(blk.ulx, blk.uly, blk.w, blk.h);
                blk = tmp;
            }

            var bakarr = (int[])blk.Data;
            // Save requested block size
            var ulx = blk.ulx;
            var uly = blk.uly;
            var w = blk.w;
            var h = blk.h;
            // Force internal data buffer to be different from external
            blk.Data = null;
            GetInternCompData(blk, c);
            // Copy the data
            if (bakarr == null)
            {
                bakarr = new int[w * h];
            }
            if (blk.offset == 0 && blk.scanw == w)
            {
                // Requested and returned block buffer are the same size
                // CONVERSION PROBLEM?
                Array.Copy((Array)blk.Data, 0, bakarr, 0, w * h);
            }
            else
            {
                // Requested and returned block are different
                for (var i = h - 1; i >= 0; i--)
                {
                    // copy line by line
                    // CONVERSION PROBLEM?
                    Array.Copy((Array)blk.Data, blk.offset + i * blk.scanw, bakarr, i * w, w);
                }
            }
            blk.Data = bakarr;
            blk.offset = 0;
            blk.scanw = blk.w;
            return blk;
        }

        /// <summary> Returns true if the data read was originally signed in the specified
        /// component, false if not. This method always returns false since PPM
        /// data is always unsigned.
        /// 
        /// </summary>
        /// <param name="compIndex">The index of the component, from 0 to N-1.
        /// 
        /// </param>
        /// <returns> always false, since PPM data is always unsigned.
        /// 
        /// </returns>
        public override bool IsOrigSigned(int compIndex)
        {
            // Check component index
            if (compIndex < 0 || compIndex > 2)
                throw new ArgumentOutOfRangeException(nameof(compIndex) + " is out of range");
            return false;
        }

        /// <summary> Returns a string of information about the object, more than 1 line
        /// long. The information string includes information from the underlying
        /// RandomAccessFile (its toString() method is called in turn).
        /// 
        /// </summary>
        /// <returns> A string of information about the object.  
        /// 
        /// </returns>
        public override string ToString()
        {
            return "ImgReaderGDI: WxH = " + w + "x" + h + ", Component = 0,1,2";
        }
    }
}