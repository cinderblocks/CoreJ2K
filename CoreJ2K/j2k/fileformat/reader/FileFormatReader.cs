/*
* cvs identifier:
*
* $Id: FileFormatReader.java,v 1.16 2002/07/25 14:04:08 grosbois Exp $
* 
* Class:                   FileFormatReader
*
* Description:             Reads the file format
*
*
*
* COPYRIGHT:
* 
* This software module was originally developed by Rapha�l Grosbois and
* Diego Santa Cruz (Swiss Federal Institute of Technology-EPFL); Joel
* Askel�f (Ericsson Radio Systems AB); and Bertrand Berthelot, David
* Bouchard, F�lix Henry, Gerard Mozelle and Patrice Onno (Canon Research
* Centre France S.A) in the course of development of the JPEG2000
* standard as specified by ISO/IEC 15444 (JPEG 2000 Standard). This
* software module is an implementation of a part of the JPEG 2000
* Standard. Swiss Federal Institute of Technology-EPFL, Ericsson Radio
* Systems AB and Canon Research Centre France S.A (collectively JJ2000
* Partners) agree not to assert against ISO/IEC and users of the JPEG
* 2000 Standard (Users) any of their rights under the copyright, not
* including other intellectual property rights, for this software module
* with respect to the usage by ISO/IEC and Users of this software module
* or modifications thereof for use in hardware or software products
* claiming conformance to the JPEG 2000 Standard. Those intending to use
* this software module in hardware or software products are advised that
* their use may infringe existing patents. The original developers of
* this software module, JJ2000 Partners and ISO/IEC assume no liability
* for use of this software module or modifications thereof. No license
* or right to this software module is granted for non JPEG 2000 Standard
* conforming products. JJ2000 Partners have full right to use this
* software module for his/her own purpose, assign or donate this
* software module to any third party and to inhibit third parties from
* using this software module for non JPEG 2000 Standard conforming
* products. This copyright notice must be included in all copies or
* derivative works of this software module.
* 
* Copyright (c) 1999/2000 JJ2000 Partners.
* */
using CoreJ2K.j2k.codestream;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.io;
using CoreJ2K.j2k.util;
using System;
using System.Collections.Generic;
using System.Text;

namespace CoreJ2K.j2k.fileformat.reader
{

    /// <summary> This class reads the file format wrapper that may or may not exist around a
    /// valid JPEG 2000 codestream. Since no information from the file format is
    /// used in the actual decoding, this class simply goes through the file and
    /// finds the first valid codestream.
    /// 
    /// </summary>
    /// <seealso cref="j2k.fileformat.writer.FileFormatWriter" />
    public class FileFormatReader
    {
        /// <summary> This method creates and returns an array of positions to contiguous
        /// codestreams in the file
        /// 
        /// </summary>
        /// <returns> The positions of the contiguous codestreams in the file
        /// 
        /// </returns>
        public virtual long[] CodeStreamPos
        {
            get
            {
                var size = codeStreamPos.Count;
                var pos = new long[size];
                for (var i = 0; i < size; i++)
                    pos[i] = codeStreamPos[i];
                return pos;
            }

        }
        /// <summary> This method returns the position of the first contiguous codestreams in
        /// the file
        /// 
        /// </summary>
        /// <returns> The position of the first contiguous codestream in the file
        /// 
        /// </returns>
        public virtual int FirstCodeStreamPos => codeStreamPos[0];

        /// <summary> This method returns the length of the first contiguous codestreams in
        /// the file
        /// 
        /// </summary>
        /// <returns> The length of the first contiguous codestream in the file
        /// 
        /// </returns>
        public virtual int FirstCodeStreamLength => codeStreamLength[0];

        /// <summary>The random access from which the file format boxes are read </summary>
        private readonly RandomAccessIO in_Renamed;

        /// <summary>The positions of the codestreams in the fileformat</summary>
        private List<int> codeStreamPos;

        /// <summary>The lengths of the codestreams in the fileformat</summary>
        private List<int> codeStreamLength;

        /// <summary>Flag indicating whether or not the JP2 file format is used </summary>
        public bool JP2FFUsed;

        /// <summary>
        /// Gets the metadata (comments, XML, UUID boxes) extracted from the file.
        /// </summary>
        public J2KMetadata Metadata { get; } = new J2KMetadata();

        /// <summary> The constructor of the FileFormatReader
        /// 
        /// </summary>
        /// <param name="in">The RandomAccessIO from which to read the file format
        /// 
        /// </param>
        public FileFormatReader(RandomAccessIO in_Renamed)
        {
            this.in_Renamed = in_Renamed;
        }

        /// <summary> This method checks whether the given RandomAccessIO is a valid JP2 file
        /// and if so finds the first codestream in the file. Currently, the
        /// information in the codestream is not used
        /// 
        /// </summary>
        /// <param name="in">The RandomAccessIO from which to read the file format
        /// 
        /// </param>
        /// <exception cref="java.io.IOException">If an I/O error ocurred.
        /// 
        /// </exception>
        /// <exception cref="java.io.EOFException">If end of file is reached
        /// 
        /// </exception>
        public virtual void readFileFormat()
        {

            //int foundCodeStreamBoxes = 0;
            int box;
            int length;
            long longLength = 0;
            int pos;
            short marker;
            var jp2HeaderBoxFound = false;
            var lastBoxFound = false;

            try
            {

                // Go through the randomaccessio and find the first contiguous
                // codestream box. Check also that the File Format is correct

                // Make sure that the first 12 bytes is the JP2_SIGNATURE_BOX or
                // if not that the first 2 bytes is the SOC marker
                if (in_Renamed.readInt() != 0x0000000c || in_Renamed.readInt() != FileFormatBoxes.JP2_SIGNATURE_BOX || in_Renamed.readInt() != 0x0d0a870a)
                {
                    // Not a JP2 file
                    in_Renamed.seek(0);

                    marker = in_Renamed.readShort();
                    if (marker != Markers.SOC)
                        //Standard syntax marker found
                        throw new InvalidOperationException("File is neither valid JP2 file nor " + "valid JPEG 2000 codestream");
                    JP2FFUsed = false;
                    in_Renamed.seek(0);
                    return;
                }

                // The JP2 File format is being used
                JP2FFUsed = true;

                // Read File Type box
                if (!readFileTypeBox())
                {
                    // Not a valid JP2 file or codestream
                    throw new InvalidOperationException("Invalid JP2 file: File Type box missing");
                }

                // Read all remaining boxes 
                while (!lastBoxFound)
                {
                    pos = in_Renamed.Pos;
                    length = in_Renamed.readInt();
                    if ((pos + length) == in_Renamed.length())
                        lastBoxFound = true;

                    box = in_Renamed.readInt();
                    if (length == 0)
                    {
                        lastBoxFound = true;
                        length = in_Renamed.length() - in_Renamed.Pos;
                    }
                    else if (length == 1)
                    {
                        longLength = in_Renamed.readLong();
                        throw new System.IO.IOException("File too long.");
                    }
                    else
                        longLength = 0;

                    switch (box)
                    {

                        case FileFormatBoxes.CONTIGUOUS_CODESTREAM_BOX:
                            if (!jp2HeaderBoxFound)
                            {
                                throw new InvalidOperationException("Invalid JP2 file: JP2Header box not " + "found before Contiguous codestream " + "box ");
                            }
                            readContiguousCodeStreamBox(pos, length, longLength);
                            break;

                        case FileFormatBoxes.JP2_HEADER_BOX:
                            if (jp2HeaderBoxFound)
                                throw new InvalidOperationException("Invalid JP2 file: Multiple " + "JP2Header boxes found");
                            readJP2HeaderBox(pos, length, longLength);
                            jp2HeaderBoxFound = true;
                            break;

                        case FileFormatBoxes.INTELLECTUAL_PROPERTY_BOX:
                            readIntPropertyBox(length);
                            break;

                        case FileFormatBoxes.XML_BOX:
                            readXMLBox(length);
                            break;

                        case FileFormatBoxes.UUID_BOX:
                            readUUIDBox(length);
                            break;

                        case FileFormatBoxes.UUID_INFO_BOX:
                            readUUIDInfoBox(length);
                            break;

                        case FileFormatBoxes.READER_REQUIREMENTS_BOX:
                            readReaderRequirementsBox(length);
                            break;

                        default:
                            FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING,
                                $"Unknown box-type: 0x{Convert.ToString(box, 16)}");
                            break;

                    }
                    if (!lastBoxFound)
                        in_Renamed.seek(pos + length);
                }
            }
            catch (System.IO.EndOfStreamException)
            {
                throw new InvalidOperationException("EOF reached before finding Contiguous " + "Codestream Box");
            }

            if (codeStreamPos.Count == 0)
            {
                // Not a valid JP2 file or codestream
                throw new InvalidOperationException("Invalid JP2 file: Contiguous codestream box " + "missing");
            }

            return;
        }

        /// <summary> This method reads the File Type box.
        /// 
        /// </summary>
        /// <returns> false if the File Type box was not found or invalid else true
        /// 
        /// </returns>
        /// <exception cref="java.io.IOException">If an I/O error ocurred.
        /// </exception>
        /// <exception cref="java.io.EOFException">If the end of file was reached
        /// 
        /// </exception>
        public virtual bool readFileTypeBox()
        {
            int length;
            long longLength = 0;
            int pos;
            int nComp;
            var foundComp = false;

            // Get current position in file
            pos = in_Renamed.Pos;

            // Read box length (LBox)
            length = in_Renamed.readInt();
            if (length == 0)
            {
                // This can not be last box
                throw new InvalidOperationException("Zero-length of Profile Box");
            }

            // Check that this is a File Type box (TBox)
            if (in_Renamed.readInt() != FileFormatBoxes.FILE_TYPE_BOX)
            {
                return false;
            }

            // Check for XLBox
            if (length == 1)
            {
                // Box has 8 byte length;
                longLength = in_Renamed.readLong();
                throw new System.IO.IOException("File too long.");
            }

            // Read Brand field
            in_Renamed.readInt();

            // Read MinV field
            in_Renamed.readInt();

            // Check that there is at least one FT_BR entry in in
            // compatibility list
            nComp = (length - 16) / 4; // Number of compatibilities.
            for (var i = nComp; i > 0; i--)
            {
                if (in_Renamed.readInt() == FileFormatBoxes.FT_BR)
                    foundComp = true;
            }
            return foundComp;
        }

        /// <summary> This method reads the JP2Header box
        /// 
        /// </summary>
        /// <param name="pos">The position in the file
        /// 
        /// </param>
        /// <param name="length">The length of the JP2Header box
        /// 
        /// </param>
        /// <param name="long">length The length of the JP2Header box if greater than
        /// 1<<32
        /// 
        /// </param>
        /// <returns> false if the JP2Header box was not found or invalid else true
        /// 
        /// </returns>
        /// <exception cref="java.io.IOException">If an I/O error ocurred.
        /// 
        /// </exception>
        /// <exception cref="java.io.EOFException">If the end of file was reached
        /// 
        /// </exception>
        public virtual bool readJP2HeaderBox(long pos, int length, long longLength)
        {

            if (length == 0)
            {
                // This can not be last box
                throw new InvalidOperationException("Zero-length of JP2Header Box");
            }

            // Read sub-boxes within JP2 Header to extract ICC profile, resolution, and channel definitions
            try
            {
                var boxHeader = new byte[16];
                var headerBoxEnd = pos + length;
                var currentPos = (int)(pos + 8); // Skip JP2 Header box header

                while (currentPos < headerBoxEnd)
                {
                    in_Renamed.seek(currentPos);
                    in_Renamed.readFully(boxHeader, 0, 8);

                    var boxLen = (boxHeader[0] << 24) | (boxHeader[1] << 16) | 
                                 (boxHeader[2] << 8) | boxHeader[3];
                    var boxType = (boxHeader[4] << 24) | (boxHeader[5] << 16) | 
                                  (boxHeader[6] << 8) | boxHeader[7];

                    // Check for Color Specification Box
                    if (boxType == FileFormatBoxes.COLOUR_SPECIFICATION_BOX)
                    {
                        // Read color specification box contents
                        var csBoxData = new byte[boxLen - 8];
                        in_Renamed.readFully(csBoxData, 0, boxLen - 8);

                        // Check method field (METH)
                        var method = csBoxData[0];

                        // Method 2 = ICC profile
                        if (method == 2)
                        {
                            // ICC profile starts at offset 3 in box data
                            // First read the profile size
                            var profileSize = (csBoxData[3] << 24) | (csBoxData[4] << 16) | 
                                            (csBoxData[5] << 8) | csBoxData[6];

                            if (profileSize > 0 && profileSize < csBoxData.Length - 3)
                            {
                                var iccProfile = new byte[profileSize];
                                Array.Copy(csBoxData, 3, iccProfile, 0, profileSize);
                                
                                // Add to metadata
                                Metadata.SetIccProfile(iccProfile);

                                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                                    $"Found ICC Profile ({profileSize} bytes)");
                            }
                        }
                    }
                    // Check for Resolution Box (superbox containing capture and/or display resolution)
                    else if (boxType == FileFormatBoxes.RESOLUTION_BOX)
                    {
                        // Resolution box is a superbox containing resc and/or resd boxes
                        var resBoxStart = currentPos + 8;
                        var resBoxEnd = currentPos + boxLen;
                        
                        readResolutionSuperBox(resBoxStart, resBoxEnd);
                    }
                    // Check for Channel Definition Box
                    else if (boxType == FileFormatBoxes.CHANNEL_DEFINITION_BOX)
                    {
                        readChannelDefinitionBox(boxLen);
                    }

                    // Move to next box
                    currentPos += boxLen;
                    if (boxLen == 0) break; // Avoid infinite loop
                }
            }
            catch (Exception e)
            {
                // Don't fail the whole operation if we can't read metadata
                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING,
                    $"Error extracting metadata from JP2 Header: {e.Message}");
            }

            return true;
        }

        /// <summary>
        /// Reads the Resolution superbox which can contain capture and/or display resolution boxes.
        /// </summary>
        /// <param name="boxStart">Start position of resolution superbox content.</param>
        /// <param name="boxEnd">End position of resolution superbox.</param>
        private void readResolutionSuperBox(int boxStart, int boxEnd)
        {
            var boxHeader = new byte[8];
            var currentPos = boxStart;

            while (currentPos < boxEnd)
            {
                in_Renamed.seek(currentPos);
                in_Renamed.readFully(boxHeader, 0, 8);

                var boxLen = (boxHeader[0] << 24) | (boxHeader[1] << 16) | 
                             (boxHeader[2] << 8) | boxHeader[3];
                var boxType = (boxHeader[4] << 24) | (boxHeader[5] << 16) | 
                              (boxHeader[6] << 8) | boxHeader[7];

                if (boxType == FileFormatBoxes.CAPTURE_RESOLUTION_BOX)
                {
                    // Read capture resolution (resc)
                    var resData = new byte[10]; // VR_N(2), VR_D(2), HR_N(2), HR_D(2), VR_E(1), HR_E(1)
                    in_Renamed.readFully(resData, 0, 10);

                    var vr_n = (short)((resData[0] << 8) | resData[1]);
                    var vr_d = (short)((resData[2] << 8) | resData[3]);
                    var hr_n = (short)((resData[4] << 8) | resData[5]);
                    var hr_d = (short)((resData[6] << 8) | resData[7]);
                    var vr_e = (sbyte)resData[8];
                    var hr_e = (sbyte)resData[9];

                    // Calculate resolution: (numerator / denominator) * 10^exponent
                    var verticalRes = (vr_n / (double)vr_d) * Math.Pow(10, vr_e);
                    var horizontalRes = (hr_n / (double)hr_d) * Math.Pow(10, hr_e);

                    if (Metadata.Resolution == null)
                        Metadata.Resolution = new ResolutionData();

                    Metadata.Resolution.SetCaptureResolution(horizontalRes, verticalRes);

                    FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                        $"Found Capture Resolution: {horizontalRes:F2}x{verticalRes:F2} pixels/meter " +
                        $"({horizontalRes / 39.3701:F2}x{verticalRes / 39.3701:F2} DPI)");
                }
                else if (boxType == FileFormatBoxes.DEFAULT_DISPLAY_RESOLUTION_BOX)
                {
                    // Read display resolution (resd)
                    var resData = new byte[10];
                    in_Renamed.readFully(resData, 0, 10);

                    var vr_n = (short)((resData[0] << 8) | resData[1]);
                    var vr_d = (short)((resData[2] << 8) | resData[3]);
                    var hr_n = (short)((resData[4] << 8) | resData[5]);
                    var hr_d = (short)((resData[6] << 8) | resData[7]);
                    var vr_e = (sbyte)resData[8];
                    var hr_e = (sbyte)resData[9];

                    var verticalRes = (vr_n / (double)vr_d) * Math.Pow(10, vr_e);
                    var horizontalRes = (hr_n / (double)hr_d) * Math.Pow(10, hr_e);

                    if (Metadata.Resolution == null)
                        Metadata.Resolution = new ResolutionData();

                    Metadata.Resolution.SetDisplayResolution(horizontalRes, verticalRes);

                    FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                        $"Found Display Resolution: {horizontalRes:F2}x{verticalRes:F2} pixels/meter " +
                        $"({horizontalRes / 39.3701:F2}x{verticalRes / 39.3701:F2} DPI)");
                }

                currentPos += boxLen;
                if (boxLen == 0) break;
            }
        }

        /// <summary>
        /// Reads the Channel Definition Box which defines the interpretation of image components.
        /// </summary>
        /// <param name="boxLength">Total length of the box including header.</param>
        private void readChannelDefinitionBox(int boxLength)
        {
            try
            {
                // Read number of channel definitions (N)
                var nDef = in_Renamed.readShort();
                
                if (nDef <= 0)
                    return;

                if (Metadata.ChannelDefinitions == null)
                    Metadata.ChannelDefinitions = new ChannelDefinitionData();

                // Read each channel definition (6 bytes each: Cn(2), Typ(2), Asoc(2))
                for (int i = 0; i < nDef; i++)
                {
                    var cn = in_Renamed.readShort();    // Channel index
                    var typ = in_Renamed.readShort();   // Channel type
                    var asoc = in_Renamed.readShort();  // Association

                    // Convert typ to ChannelType enum
                    var channelType = ChannelType.Unspecified;
                    switch (typ)
                    {
                        case 0:
                            channelType = ChannelType.Color;
                            break;
                        case 1:
                            channelType = ChannelType.Opacity;
                            break;
                        case 2:
                            channelType = ChannelType.PremultipliedOpacity;
                            break;
                        default:
                            channelType = ChannelType.Unspecified;
                            break;
                    }

                    Metadata.ChannelDefinitions.AddChannel(cn, channelType, asoc);
                }

                var alphaInfo = Metadata.ChannelDefinitions.HasAlphaChannel ? " (with alpha)" : "";
                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                    $"Found Channel Definition Box: {nDef} channels{alphaInfo}");
            }
            catch (Exception e)
            {
                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING,
                    $"Error reading Channel Definition Box: {e.Message}");
            }
        }

        /// <summary> This method skips the Contiguous codestream box and adds position
        /// of contiguous codestream to a vector
        /// 
        /// </summary>
        /// <param name="pos">The position in the file
        /// 
        /// </param>
        /// <param name="length">The length of the JP2Header box
        /// 
        /// </param>
        /// <param name="long">length The length of the JP2Header box if greater than 1<<32
        /// 
        /// </param>
        /// <returns> false if the Contiguous codestream box was not found or invalid
        /// else true
        /// 
        /// </returns>
        /// <exception cref="java.io.IOException">If an I/O error ocurred.
        /// 
        /// </exception>
        /// <exception cref="java.io.EOFException">If the end of file was reached
        /// 
        /// </exception>
        public virtual bool readContiguousCodeStreamBox(long pos, int length, long longLength)
        {

            // Add new codestream position to position vector
            var ccpos = in_Renamed.Pos;

            if (codeStreamPos == null)
                codeStreamPos = new List<int>(10);
            codeStreamPos.Add(ccpos);

            // Add new codestream length to length vector
            if (codeStreamLength == null)
                codeStreamLength = new List<int>(10);
            codeStreamLength.Add(length);

            return true;
        }

        /// <summary> This method reads the contents of the Intellectual property box
        /// 
        /// </summary>
        public virtual void readIntPropertyBox(int length)
        {
        }

        /// <summary> This method reads the contents of the XML box
        /// 
        /// </summary>
        public virtual void readXMLBox(int length)
        {
            if (length <= 8) return; // Box too small

            try
            {
                // Read the XML content (length includes 8-byte box header)
                var dataLength = length - 8;
                var xmlBytes = new byte[dataLength];
                in_Renamed.readFully(xmlBytes, 0, dataLength);

                // Convert to string (XML boxes are UTF-8)
                var xmlContent = Encoding.UTF8.GetString(xmlBytes);

                // Add to metadata
                Metadata.XmlBoxes.Add(new XmlBox { XmlContent = xmlContent });

                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                    $"Found XML box ({dataLength} bytes)");
            }
            catch (Exception e)
            {
                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING,
                    $"Error reading XML box: {e.Message}");
            }
        }

        /// <summary> This method reads the contents of the UUID box
        /// 
        /// </summary>
        public virtual void readUUIDBox(int length)
        {
            if (length <= 24) return; // Box too small (8 header + 16 UUID)

            try
            {
                // Read UUID (16 bytes)
                var uuidBytes = new byte[16];
                in_Renamed.readFully(uuidBytes, 0, 16);
                var uuid = new Guid(uuidBytes);

                // Read data (remaining bytes)
                var dataLength = length - 24; // Box header (8) + UUID (16)
                var data = new byte[dataLength];
                in_Renamed.readFully(data, 0, dataLength);

                // Add to metadata
                Metadata.UuidBoxes.Add(new UuidBox { Uuid = uuid, Data = data });

                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.INFO,
                    $"Found UUID box: {uuid} ({dataLength} bytes)");
            }
            catch (Exception e)
            {
                FacilityManager.getMsgLogger().printmsg(MsgLogger_Fields.WARNING,
                    $"Error reading UUID box: {e.Message}");
            }
        }

        /// <summary> This method reads the contents of the UUID Info box
        /// 
        /// </summary>
        public virtual void readUUIDInfoBox(int length)
        {
        }

        /// <summary> This method reads the contents of the Reader requirements box
        /// 
        /// </summary>
        public virtual void readReaderRequirementsBox(int length)
        {
        }
    }
}