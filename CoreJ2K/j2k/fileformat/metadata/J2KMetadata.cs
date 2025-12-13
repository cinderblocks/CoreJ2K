// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Text;
using CoreJ2K.Color.ICC;

namespace CoreJ2K.j2k.fileformat.metadata
{
    /// <summary>
    /// Represents metadata extracted from or to be written to a JPEG2000 file.
    /// Supports comments, XML boxes (XMP, IPTC), UUID boxes, ICC profiles, resolution data, and channel definitions.
    /// </summary>
    public class J2KMetadata
    {
        /// <summary>
        /// Gets the list of text comments found in the file.
        /// </summary>
        public List<CommentBox> Comments { get; } = new List<CommentBox>();

        /// <summary>
        /// Gets the list of XML boxes (including XMP, IPTC, etc.).
        /// </summary>
        public List<XmlBox> XmlBoxes { get; } = new List<XmlBox>();

        /// <summary>
        /// Gets the list of UUID boxes with custom vendor data.
        /// </summary>
        public List<UuidBox> UuidBoxes { get; } = new List<UuidBox>();

        /// <summary>
        /// Gets or sets the ICC color profile data.
        /// </summary>
        public ICCProfileData IccProfile { get; set; }

        /// <summary>
        /// Gets or sets the resolution metadata (DPI/PPI information).
        /// </summary>
        public ResolutionData Resolution { get; set; }

        /// <summary>
        /// Gets or sets the channel definition metadata (alpha channel, component types).
        /// </summary>
        public ChannelDefinitionData ChannelDefinitions { get; set; }

        /// <summary>
        /// Adds a simple text comment to the metadata.
        /// </summary>
        public void AddComment(string text, string language = "en")
        {
            Comments.Add(new CommentBox { Text = text, Language = language });
        }

        /// <summary>
        /// Adds XML content (e.g., XMP, IPTC) to the metadata.
        /// </summary>
        public void AddXml(string xmlContent)
        {
            XmlBoxes.Add(new XmlBox { XmlContent = xmlContent });
        }

        /// <summary>
        /// Adds a UUID box with custom binary data.
        /// </summary>
        public void AddUuid(Guid uuid, byte[] data)
        {
            UuidBoxes.Add(new UuidBox { Uuid = uuid, Data = data });
        }

        /// <summary>
        /// Sets the ICC profile from raw profile bytes.
        /// </summary>
        /// <param name="profileBytes">The ICC profile bytes.</param>
        public void SetIccProfile(byte[] profileBytes)
        {
            IccProfile = new ICCProfileData(profileBytes);
        }

        /// <summary>
        /// Sets the resolution from DPI values.
        /// Creates a ResolutionData instance if needed.
        /// </summary>
        /// <param name="horizontalDpi">Horizontal DPI.</param>
        /// <param name="verticalDpi">Vertical DPI.</param>
        /// <param name="isCapture">True for capture resolution, false for display resolution.</param>
        public void SetResolutionDpi(double horizontalDpi, double verticalDpi, bool isCapture = false)
        {
            if (Resolution == null)
                Resolution = new ResolutionData();

            if (isCapture)
                Resolution.SetCaptureDpi(horizontalDpi, verticalDpi);
            else
                Resolution.SetDisplayDpi(horizontalDpi, verticalDpi);
        }

        /// <summary>
        /// Gets the first XMP metadata box, if present.
        /// </summary>
        public XmlBox GetXmp()
        {
            return XmlBoxes.Find(x => x.IsXMP);
        }

        /// <summary>
        /// Gets the first IPTC metadata box, if present.
        /// </summary>
        public XmlBox GetIptc()
        {
            return XmlBoxes.Find(x => x.IsIPTC);
        }
    }

    /// <summary>
    /// Represents a text comment box (XML box with plain text or COM marker segment).
    /// </summary>
    public class CommentBox
    {
        /// <summary>
        /// Gets or sets the comment text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the ISO 639 language code (e.g., "en", "fr", "de").
        /// </summary>
        public string Language { get; set; } = "en";

        /// <summary>
        /// Gets or sets whether this comment contains binary data (not UTF-8 text).
        /// </summary>
        public bool IsBinary { get; set; }

        public override string ToString()
        {
            return $"Comment[{Language}]: {Text?.Substring(0, Math.Min(50, Text?.Length ?? 0))}...";
        }
    }

    /// <summary>
    /// Represents an XML box containing structured metadata (XMP, IPTC, etc.).
    /// </summary>
    public class XmlBox
    {
        /// <summary>
        /// Gets or sets the XML content as a string.
        /// </summary>
        public string XmlContent { get; set; }

        /// <summary>
        /// Returns true if this appears to be an XMP box.
        /// </summary>
        public bool IsXMP => XmlContent?.Contains("x:xmpmeta") == true 
                          || XmlContent?.Contains("xmpmeta") == true
                          || XmlContent?.Contains("rdf:RDF") == true;

        /// <summary>
        /// Returns true if this appears to be an IPTC box.
        /// </summary>
        public bool IsIPTC => XmlContent?.Contains("iptc") == true
                           || XmlContent?.Contains("Iptc4xmpCore") == true;

        public override string ToString()
        {
            var type = IsXMP ? "XMP" : IsIPTC ? "IPTC" : "XML";
            return $"{type} Box ({XmlContent?.Length ?? 0} chars)";
        }
    }

    /// <summary>
    /// Represents a UUID box containing vendor-specific binary data.
    /// </summary>
    public class UuidBox
    {
        /// <summary>
        /// Gets or sets the UUID identifying the data format/vendor.
        /// </summary>
        public Guid Uuid { get; set; }

        /// <summary>
        /// Gets or sets the binary payload data.
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// Well-known UUID for XMP metadata stored in UUID box.
        /// </summary>
        public static readonly Guid XmpUuid = new Guid("be7acfcb-97a9-42e8-9c71-999491e3afac");

        /// <summary>
        /// Well-known UUID for EXIF metadata (JpgTiffExif format).
        /// </summary>
        public static readonly Guid ExifUuid = new Guid("4a504720-0d0a-870a-0000-000000000000");

        /// <summary>
        /// Returns true if this is a known XMP UUID box.
        /// </summary>
        public bool IsXmp => Uuid == XmpUuid;

        /// <summary>
        /// Returns true if this is a known EXIF UUID box.
        /// </summary>
        public bool IsExif => Uuid == ExifUuid;

        /// <summary>
        /// Gets the data as UTF-8 text if appropriate (e.g., for XMP in UUID).
        /// </summary>
        public string GetTextData()
        {
            try
            {
                return Encoding.UTF8.GetString(Data);
            }
            catch
            {
                return null;
            }
        }

        public override string ToString()
        {
            var name = IsXmp ? "XMP" : IsExif ? "EXIF" : "UUID";
            return $"{name} Box [{Uuid}] ({Data?.Length ?? 0} bytes)";
        }
    }
}
