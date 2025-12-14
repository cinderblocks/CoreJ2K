// Copyright (c) 2025 Sjofn LLC.
// Licensed under the BSD 3-Clause License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CoreJ2K.Color.ICC;
using CoreJ2K.j2k.codestream.metadata;

namespace CoreJ2K.j2k.fileformat.metadata
{
    /// <summary>
    /// Represents metadata extracted from or to be written to a JPEG2000 file.
    /// Supports comments, XML boxes (XMP, IPTC), UUID boxes, ICC profiles, resolution data, channel definitions, palette, component mapping, TLM data, JPR, and Label boxes.
    /// </summary>
    public class J2KMetadata
    {
        /// <summary>
        /// Gets the list of text comments found in the file.
        /// </summary>
        public List<CommentBox> Comments { get; } = new List<CommentBox>();

        /// <summary>
        /// Gets the list of comments from COM (Comment) marker segments in the codestream.
        /// These are stored in the main header or tile-part headers.
        /// </summary>
        public List<CodestreamComment> CodestreamComments { get; } = new List<CodestreamComment>();

        /// <summary>
        /// Gets the list of XML boxes (including XMP, IPTC, etc.).
        /// </summary>
        public List<XmlBox> XmlBoxes { get; } = new List<XmlBox>();

        /// <summary>
        /// Gets the list of UUID boxes with custom vendor data.
        /// </summary>
        public List<UuidBox> UuidBoxes { get; } = new List<UuidBox>();

        /// <summary>
        /// Gets the list of Intellectual Property Rights (JPR) boxes from JPEG 2000 Part 2.
        /// </summary>
        public List<JprBox> IntellectualPropertyRights { get; } = new List<JprBox>();

        /// <summary>
        /// Gets the list of Label boxes from JPEG 2000 Part 2.
        /// </summary>
        public List<LabelBox> Labels { get; } = new List<LabelBox>();

        /// <summary>
        /// Gets or sets the UUID Info box data (contains UUID list and URL boxes).
        /// </summary>
        public UuidInfoBox UuidInfo { get; set; }

        /// <summary>
        /// Gets or sets the Reader Requirements box data (defines required decoder capabilities).
        /// </summary>
        public ReaderRequirementsBox ReaderRequirements { get; set; }

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
        /// Gets or sets the tile-part lengths data (TLM marker information) for fast tile access.
        /// </summary>
        public TilePartLengthsData TilePartLengths { get; set; }

        /// <summary>
        /// Gets or sets the palette box data (for palettized/indexed color images).
        /// </summary>
        public PaletteData Palette { get; set; }

        /// <summary>
        /// Gets or sets the component mapping box data (maps codestream components to image channels).
        /// </summary>
        public ComponentMappingData ComponentMapping { get; set; }

        /// <summary>
        /// Gets or sets the bits per component box data (varying bit depths per component).
        /// Required when Image Header Box BPC field is 0xFF (components have different bit depths).
        /// </summary>
        public BitsPerComponentData BitsPerComponent { get; set; }

        /// <summary>
        /// Gets or sets the component registration data (CRG marker from codestream).
        /// Specifies sub-pixel offsets for precise component spatial registration.
        /// Per ISO/IEC 15444-1 Annex A.11.3.
        /// </summary>
        public ComponentRegistrationData ComponentRegistration { get; set; }

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
        /// Adds an Intellectual Property Rights box (JPEG 2000 Part 2).
        /// </summary>
        /// <param name="text">The copyright or rights statement.</param>
        public void AddIntellectualPropertyRights(string text)
        {
            IntellectualPropertyRights.Add(new JprBox { Text = text });
        }

        /// <summary>
        /// Adds a Label box (JPEG 2000 Part 2).
        /// </summary>
        /// <param name="label">The label text.</param>
        public void AddLabel(string label)
        {
            Labels.Add(new LabelBox { Label = label });
        }

        /// <summary>
        /// Adds a COM marker comment from the codestream.
        /// </summary>
        /// <param name="text">The comment text</param>
        /// <param name="registrationMethod">Registration method (0=binary, 1=Latin text)</param>
        /// <param name="isMainHeader">True if from main header, false if from tile header</param>
        /// <param name="tileIndex">Tile index (if from tile header)</param>
        public void AddCodestreamComment(string text, int registrationMethod = 1, bool isMainHeader = true, int tileIndex = -1)
        {
            CodestreamComments.Add(new CodestreamComment
            {
                Text = text,
                RegistrationMethod = registrationMethod,
                IsMainHeader = isMainHeader,
                TileIndex = tileIndex
            });
        }

        /// <summary>
        /// Adds a COM marker comment with binary data from the codestream.
        /// </summary>
        /// <param name="data">The binary comment data</param>
        /// <param name="registrationMethod">Registration method</param>
        /// <param name="isMainHeader">True if from main header, false if from tile header</param>
        /// <param name="tileIndex">Tile index (if from tile header)</param>
        public void AddCodestreamComment(byte[] data, int registrationMethod, bool isMainHeader = true, int tileIndex = -1)
        {
            CodestreamComments.Add(new CodestreamComment
            {
                Data = data,
                RegistrationMethod = registrationMethod,
                IsMainHeader = isMainHeader,
                TileIndex = tileIndex,
                IsBinary = true
            });
        }

        /// <summary>
        /// Gets all comments from both JP2 boxes and codestream COM markers.
        /// </summary>
        public IEnumerable<string> GetAllComments()
        {
            foreach (var comment in Comments)
            {
                if (!string.IsNullOrEmpty(comment.Text))
                    yield return comment.Text;
            }

            foreach (var comment in CodestreamComments)
            {
                if (!comment.IsBinary && !string.IsNullOrEmpty(comment.Text))
                    yield return comment.Text;
            }
        }

        /// <summary>
        /// Gets all comments from the main header COM markers.
        /// </summary>
        public IEnumerable<CodestreamComment> GetMainHeaderComments()
        {
            return CodestreamComments.Where(c => c.IsMainHeader);
        }

        /// <summary>
        /// Gets all comments from a specific tile's COM markers.
        /// </summary>
        /// <param name="tileIndex">The tile index</param>
        public IEnumerable<CodestreamComment> GetTileComments(int tileIndex)
        {
            return CodestreamComments.Where(c => !c.IsMainHeader && c.TileIndex == tileIndex);
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
        /// Sets palette data for indexed color images.
        /// </summary>
        /// <param name="numEntries">Number of palette entries.</param>
        /// <param name="numColumns">Number of color columns (typically 3 for RGB).</param>
        /// <param name="bitDepths">Bit depth for each column (sign bit in MSB).</param>
        /// <param name="entries">The palette entries [entry][column].</param>
        public void SetPalette(int numEntries, int numColumns, short[] bitDepths, int[][] entries)
        {
            Palette = new PaletteData
            {
                NumEntries = numEntries,
                NumColumns = numColumns,
                BitDepths = bitDepths,
                Entries = entries
            };
        }

        /// <summary>
        /// Adds a component mapping entry (maps a codestream component to an output channel).
        /// </summary>
        /// <param name="componentIndex">Codestream component index.</param>
        /// <param name="mappingType">Mapping type (0=direct, 1=palette).</param>
        /// <param name="paletteColumn">Palette column index (if mappingType=1).</param>
        public void AddComponentMapping(ushort componentIndex, byte mappingType, byte paletteColumn)
        {
            if (ComponentMapping == null)
                ComponentMapping = new ComponentMappingData();

            ComponentMapping.AddMapping(componentIndex, mappingType, paletteColumn);
        }

        /// <summary>
        /// Sets component registration offsets for precise spatial positioning.
        /// </summary>
        /// <param name="numComponents">Number of components.</param>
        /// <param name="horizontalOffsets">Horizontal offsets in units of 1/65536 of sample separation.</param>
        /// <param name="verticalOffsets">Vertical offsets in units of 1/65536 of sample separation.</param>
        public void SetComponentRegistration(int numComponents, int[] horizontalOffsets = null, int[] verticalOffsets = null)
        {
            ComponentRegistration = ComponentRegistrationData.Create(numComponents, horizontalOffsets, verticalOffsets);
        }

        /// <summary>
        /// Sets component registration with standard chroma positioning.
        /// </summary>
        /// <param name="numComponents">Number of components.</param>
        /// <param name="chromaPosition">Chroma positioning: 0=centered, 1=co-sited.</param>
        public void SetChromaPosition(int numComponents, int chromaPosition)
        {
            ComponentRegistration = ComponentRegistrationData.CreateWithChromaPosition(numComponents, chromaPosition);
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

    /// <summary>
    /// Represents an Intellectual Property Rights (JPR) box from JPEG 2000 Part 2.
    /// The JPR box contains copyright or other intellectual property rights information.
    /// This box supersedes the IPR flag in the Image Header box from Part 1.
    /// </summary>
    public class JprBox
    {
        /// <summary>
        /// Gets or sets the intellectual property rights statement (e.g., copyright notice).
        /// This is stored as UTF-8 text.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Gets or sets the raw binary data. 
        /// When set, this takes precedence over Text property.
        /// </summary>
        public byte[] RawData { get; set; }

        /// <summary>
        /// Returns true if this box contains binary data rather than text.
        /// </summary>
        public bool IsBinary => RawData != null;

        /// <summary>
        /// Gets the text content, converting from RawData if necessary.
        /// </summary>
        public string GetText()
        {
            if (!string.IsNullOrEmpty(Text))
                return Text;

            if (RawData != null)
            {
                try
                {
                    return Encoding.UTF8.GetString(RawData);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public override string ToString()
        {
            var text = GetText();
            var preview = text?.Length > 50 ? text.Substring(0, 50) + "..." : text;
            return $"JPR Box: {preview ?? "(binary data)"}";
        }
    }

    /// <summary>
    /// Represents a Label (LBL) box from JPEG 2000 Part 2.
    /// The Label box contains human-readable text labels for the image or components.
    /// This can be used to provide descriptions, titles, or other labeling information.
    /// </summary>
    public class LabelBox
    {
        /// <summary>
        /// Gets or sets the label text.
        /// Labels are stored as UTF-8 text without null termination.
        /// </summary>
        public string Label { get; set; }

        /// <summary>
        /// Gets or sets the raw binary data.
        /// When set, this takes precedence over Label property.
        /// </summary>
        public byte[] RawData { get; set; }

        /// <summary>
        /// Returns true if this box contains binary data rather than text.
        /// </summary>
        public bool IsBinary => RawData != null;

        /// <summary>
        /// Gets the label text, converting from RawData if necessary.
        /// </summary>
        public string GetLabel()
        {
            if (!string.IsNullOrEmpty(Label))
                return Label;

            if (RawData != null)
            {
                try
                {
                    return Encoding.UTF8.GetString(RawData);
                }
                catch
                {
                    return null;
                }
            }

            return null;
        }

        public override string ToString()
        {
            var label = GetLabel();
            var preview = label?.Length > 50 ? label.Substring(0, 50) + "..." : label;
            return $"Label Box: {preview ?? "(binary data)"}";
        }
    }

    /// <summary>
    /// Represents a UUID Info (uinf) box from JPEG 2000 Part 1.
    /// The UUID Info box is a superbox that contains a UUID List box and optionally a URL box.
    /// It provides information about UUIDs used in the file and where to find more information.
    /// </summary>
    public class UuidInfoBox
    {
        /// <summary>
        /// Gets the list of UUIDs referenced in the file.
        /// </summary>
        public List<Guid> UuidList { get; } = new List<Guid>();

        /// <summary>
        /// Gets or sets the URL where more information about the UUIDs can be found.
        /// </summary>
        public string Url { get; set; }

        /// <summary>
        /// Gets or sets the version number of the URL.
        /// </summary>
        public byte UrlVersion { get; set; }

        /// <summary>
        /// Gets or sets the URL flags (0 = relative URL,  1 = absolute URL).
        /// </summary>
        public byte UrlFlags { get; set; }

        public override string ToString()
        {
            var uuidCount = UuidList.Count;
            var urlInfo = !string.IsNullOrEmpty(Url) ? $", URL: {Url}" : "";
            return $"UUID Info Box: {uuidCount} UUID(s){urlInfo}";
        }
    }

    /// <summary>
    /// Represents a Reader Requirements (rreq) box from JPEG 2000 Part 1.
    /// The Reader Requirements box specifies what features a decoder must support to properly decode the file.
    /// This allows decoders to quickly determine if they can handle a file before attempting to decode it.
    /// </summary>
    public class ReaderRequirementsBox
    {
        /// <summary>
        /// Gets the list of standard features (Feature IDs) that the reader must support.
        /// Each feature ID is a 16-bit value defined in ISO/IEC 15444-1 Annex I.
        /// </summary>
        public List<ushort> StandardFeatures { get; } = new List<ushort>();

        /// <summary>
        /// Gets the list of vendor-specific features that the reader must support.
        /// Each vendor feature is identified by a UUID.
        /// </summary>
        public List<Guid> VendorFeatures { get; } = new List<Guid>();

        /// <summary>
        /// Gets or sets whether the file is fully compatible with JPEG 2000 Part 1 baseline.
        /// </summary>
        public bool IsJp2Compatible { get; set; }

        /// <summary>
        /// Checks if a specific standard feature is required.
        /// </summary>
        /// <param name="featureId">The feature ID to check.</param>
        /// <returns>True if the feature is required.</returns>
        public bool RequiresFeature(ushort featureId)
        {
            return StandardFeatures.Contains(featureId);
        }

        /// <summary>
        /// Checks if a specific vendor feature is required.
        /// </summary>
        /// <param name="uuid">The vendor feature UUID to check.</param>
        /// <returns>True if the vendor feature is required.</returns>
        public bool RequiresVendorFeature(Guid uuid)
        {
            return VendorFeatures.Contains(uuid);
        }

        public override string ToString()
        {
            var stdCount = StandardFeatures.Count;
            var vendorCount = VendorFeatures.Count;
            var compat = IsJp2Compatible ? " (JP2 compatible)" : "";
            return $"Reader Requirements Box: {stdCount} standard feature(s), {vendorCount} vendor feature(s){compat}";
        }
    }

    /// <summary>
    /// Represents palette (pclr) box data for indexed color images.
    /// The palette maps index values to multi-component color values.
    /// Required when using palettized color in JP2 images.
    /// </summary>
    public class PaletteData
    {
        /// <summary>
        /// Gets or sets the number of palette entries (NE field).
        /// Valid range: 1 to 1024 for most implementations.
        /// </summary>
        public int NumEntries { get; set; }

        /// <summary>
        /// Gets or sets the number of palette columns/components (NPC field).
        /// Typically 3 for RGB palettes, 1 for grayscale palettes.
        /// </summary>
        public int NumColumns { get; set; }

        /// <summary>
        /// Gets or sets the bit depths for each column (B field).
        /// Format: bits 0-6 = bit depth minus 1, bit 7 = sign bit (1=signed, 0=unsigned).
        /// Array length must equal NumColumns.
        /// </summary>
        public short[] BitDepths { get; set; }

        /// <summary>
        /// Gets or sets the palette entries.
        /// Format: entries[entryIndex][columnIndex]
        /// Each entry maps an index to color component values.
        /// </summary>
        public int[][] Entries { get; set; }

        /// <summary>
        /// Returns true if the specified column uses signed values.
        /// </summary>
        public bool IsSigned(int column)
        {
            return (BitDepths[column] & 0x80) != 0;
        }

        /// <summary>
        /// Gets the bit depth for a column (without the sign bit).
        /// </summary>
        public int GetBitDepth(int column)
        {
            return (BitDepths[column] & 0x7F) + 1;
        }

        /// <summary>
        /// Gets a palette entry value.
        /// </summary>
        public int GetEntry(int entryIndex, int columnIndex)
        {
            return Entries[entryIndex][columnIndex];
        }

        public override string ToString()
        {
            var depths = new StringBuilder();
            for (int i = 0; i < NumColumns; i++)
            {
                if (i > 0) depths.Append(", ");
                depths.Append($"{GetBitDepth(i)}{(IsSigned(i) ? "S" : "U")}");
            }
            return $"Palette Box: {NumEntries} entries, {NumColumns} columns, depths=[{depths}]";
        }
    }

    /// <summary>
    /// Represents component mapping (cmap) box data.
    /// Maps codestream components to output image channels, with optional palette indirection.
    /// Required when using palettized color or when components need custom channel assignments.
    /// </summary>
    public class ComponentMappingData
    {
        /// <summary>
        /// Gets the list of component mappings.
        /// </summary>
        public List<ComponentMapping> Mappings { get; } = new List<ComponentMapping>();

        /// <summary>
        /// Gets the number of mapped channels.
        /// </summary>
        public int NumChannels => Mappings.Count;

        /// <summary>
        /// Adds a component mapping.
        /// </summary>
        /// <param name="componentIndex">Codestream component index (CMP field).</param>
        /// <param name="mappingType">Mapping type (MTYP field): 0=direct, 1=palette mapping.</param>
        /// <param name="paletteColumn">Palette column index (PCOL field), used when mappingType=1.</param>
        public void AddMapping(ushort componentIndex, byte mappingType, byte paletteColumn)
        {
            Mappings.Add(new ComponentMapping
            {
                ComponentIndex = componentIndex,
                MappingType = mappingType,
                PaletteColumn = paletteColumn
            });
        }

        /// <summary>
        /// Gets the component index for a channel.
        /// </summary>
        public ushort GetComponentIndex(int channel)
        {
            return Mappings[channel].ComponentIndex;
        }

        /// <summary>
        /// Gets the mapping type for a channel.
        /// </summary>
        public byte GetMappingType(int channel)
        {
            return Mappings[channel].MappingType;
        }

        /// <summary>
        /// Gets the palette column for a channel.
        /// </summary>
        public byte GetPaletteColumn(int channel)
        {
            return Mappings[channel].PaletteColumn;
        }

        /// <summary>
        /// Returns true if any channel uses palette mapping.
        /// </summary>
        public bool UsesPalette => Mappings.Exists(m => m.MappingType == 1);

        public override string ToString()
        {
            var sb = new StringBuilder($"Component Mapping Box: {NumChannels} channels");
            for (int i = 0; i < Mappings.Count; i++)
            {
                var m = Mappings[i];
                sb.Append($"\n  Channel[{i}]: Component={m.ComponentIndex}, Type={m.MappingType}");
                if (m.MappingType == 1)
                    sb.Append($", PaletteCol={m.PaletteColumn}");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Represents a single component-to-channel mapping entry.
    /// </summary>
    public class ComponentMapping
    {
        /// <summary>
        /// Gets or sets the codestream component index (CMP field).
        /// </summary>
        public ushort ComponentIndex { get; set; }

        /// <summary>
        /// Gets or sets the mapping type (MTYP field).
        /// 0 = Direct use (component maps directly to channel)
        /// 1 = Palette mapping (component used as index into palette)
        /// </summary>
        public byte MappingType { get; set; }

        /// <summary>
        /// Gets or sets the palette column index (PCOL field).
        /// Only used when MappingType = 1.
        /// Specifies which column of the palette to use.
        /// </summary>
        public byte PaletteColumn { get; set; }
    }

    /// <summary>
    /// Represents bits per component (bpcc) box data per ISO/IEC 15444-1 Section I.5.3.2.
    /// This box specifies the bit depth for each component when they vary across components.
    /// Required when the Image Header Box BPC field is 0xFF (indicating varying bit depths).
    /// </summary>
    public class BitsPerComponentData
    {
        /// <summary>
        /// Gets or sets the bit depth specification for each component.
        /// Format: bits 0-6 = bit depth minus 1, bit 7 = sign bit (1=signed, 0=unsigned).
        /// </summary>
        public byte[] ComponentBitDepths { get; set; }

        /// <summary>
        /// Gets the number of components.
        /// </summary>
        public int NumComponents => ComponentBitDepths?.Length ?? 0;

        /// <summary>
        /// Returns true if the specified component uses signed values.
        /// </summary>
        /// <param name="componentIndex">The component index (0-based).</param>
        public bool IsSigned(int componentIndex)
        {
            if (ComponentBitDepths == null || componentIndex < 0 || componentIndex >= ComponentBitDepths.Length)
                return false;
            return (ComponentBitDepths[componentIndex] & 0x80) != 0;
        }

        /// <summary>
        /// Gets the bit depth for a component (without the sign bit).
        /// </summary>
        /// <param name="componentIndex">The component index (0-based).</param>
        public int GetBitDepth(int componentIndex)
        {
            if (ComponentBitDepths == null || componentIndex < 0 || componentIndex >= ComponentBitDepths.Length)
                return 0;
            return (ComponentBitDepths[componentIndex] & 0x7F) + 1;
        }

        /// <summary>
        /// Sets the bit depth for a component.
        /// </summary>
        /// <param name="componentIndex">The component index (0-based).</param>
        /// <param name="bitDepth">The bit depth (1-38).</param>
        /// <param name="isSigned">Whether the component uses signed values.</param>
        public void SetBitDepth(int componentIndex, int bitDepth, bool isSigned)
        {
            if (ComponentBitDepths == null || componentIndex < 0 || componentIndex >= ComponentBitDepths.Length)
                throw new ArgumentOutOfRangeException(nameof(componentIndex));

            if (bitDepth < 1 || bitDepth > 38)
                throw new ArgumentOutOfRangeException(nameof(bitDepth), "Bit depth must be between 1 and 38");

            byte value = (byte)((bitDepth - 1) & 0x7F);
            if (isSigned)
                value |= 0x80;

            ComponentBitDepths[componentIndex] = value;
        }

        /// <summary>
        /// Checks if all components have the same bit depth and signedness.
        /// </summary>
        /// <returns>True if all components are uniform, false if they vary.</returns>
        public bool AreComponentsUniform()
        {
            if (ComponentBitDepths == null || ComponentBitDepths.Length <= 1)
                return true;

            var firstValue = ComponentBitDepths[0];
            for (int i = 1; i < ComponentBitDepths.Length; i++)
            {
                if (ComponentBitDepths[i] != firstValue)
                    return false;
            }

            return true;
        }

        /// <summary>
        /// Checks if a Bits Per Component box is needed (per ISO/IEC 15444-1 Section I.5.3.2).
        /// Returns true if components have varying bit depths or signedness.
        /// </summary>
        public bool IsBoxNeeded()
        {
            return !AreComponentsUniform();
        }

        /// <summary>
        /// Creates a BitsPerComponentData instance from an array of bit depth values.
        /// </summary>
        /// <param name="bitDepths">Array of bit depths for each component.</param>
        /// <param name="isSigned">Array indicating if each component is signed.</param>
        public static BitsPerComponentData FromBitDepths(int[] bitDepths, bool[] isSigned)
        {
            if (bitDepths == null)
                throw new ArgumentNullException(nameof(bitDepths));
            if (isSigned != null && isSigned.Length != bitDepths.Length)
                throw new ArgumentException("isSigned array must match bitDepths array length");

            var data = new BitsPerComponentData
            {
                ComponentBitDepths = new byte[bitDepths.Length]
            };

            for (int i = 0; i < bitDepths.Length; i++)
            {
                data.SetBitDepth(i, bitDepths[i], isSigned?[i] ?? false);
            }

            return data;
        }

        public override string ToString()
        {
            if (ComponentBitDepths == null || ComponentBitDepths.Length == 0)
                return "Bits Per Component Box: No components";

            var sb = new StringBuilder($"Bits Per Component Box: {NumComponents} components - ");
            for (int i = 0; i < ComponentBitDepths.Length && i < 10; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append($"C{i}:{GetBitDepth(i)}{(IsSigned(i) ? "S" : "U")}");
            }
            if (ComponentBitDepths.Length > 10)
                sb.Append("...");

            return sb.ToString();
        }
    }
}
