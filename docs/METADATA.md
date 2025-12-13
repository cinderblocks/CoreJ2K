# JPEG2000 Metadata Support in CoreJ2K

CoreJ2K now supports reading and writing metadata (XML boxes, UUID boxes, and comments) in JPEG2000 files, conforming to ISO/IEC 15444-1 (JPEG2000 Part 1).

## Features

- ? **XML Boxes** - Store structured metadata (XMP, IPTC, etc.)
- ? **UUID Boxes** - Store vendor-specific binary data
- ? **Comment Support** - Text comments with language codes
- ? **XMP Detection** - Automatic identification of XMP metadata
- ? **IPTC Detection** - Automatic identification of IPTC metadata
- ? **Well-Known UUIDs** - Support for standard UUID identifiers (XMP, EXIF)

## Usage Examples

### Reading Metadata from a JPEG2000 File

```csharp
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;
using System.IO;

// Decode image and extract metadata
using (var stream = File.OpenRead("image.jp2"))
{
    var image = J2kImage.FromStream(stream, out J2KMetadata metadata);
    
    // Access comments
    foreach (var comment in metadata.Comments)
    {
        Console.WriteLine($"Comment [{comment.Language}]: {comment.Text}");
    }
    
    // Access XMP metadata
    var xmp = metadata.GetXmp();
    if (xmp != null)
    {
        Console.WriteLine("XMP Metadata found:");
        Console.WriteLine(xmp.XmlContent);
    }
    
    // Access IPTC metadata
    var iptc = metadata.GetIptc();
    if (iptc != null)
    {
        Console.WriteLine("IPTC Metadata found");
    }
    
    // Access UUID boxes
    foreach (var uuid in metadata.UuidBoxes)
    {
        Console.WriteLine($"UUID: {uuid.Uuid}");
        if (uuid.IsXmp)
        {
            Console.WriteLine($"XMP Data: {uuid.GetTextData()}");
        }
    }
}
```

### Writing Metadata to a JPEG2000 File

```csharp
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.image;

// Create metadata
var metadata = new J2KMetadata();

// Add a simple comment
metadata.AddComment("Created with CoreJ2K", "en");

// Add XMP metadata
var xmpXml = @"<?xml version=""1.0""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"">
      <dc:title>My Image Title</dc:title>
      <dc:creator>John Doe</dc:creator>
      <dc:description>A beautiful landscape photo</dc:description>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>";
metadata.AddXml(xmpXml);

// Add a custom UUID box
var customUuid = new Guid("12345678-1234-5678-1234-567812345678");
var customData = System.Text.Encoding.UTF8.GetBytes("Version 1.0");
metadata.AddUuid(customUuid, customData);

// Encode image with metadata
BlkImgDataSrc imageSource = ...; // your image source
var encoded = J2kImage.ToBytes(imageSource, metadata);

// Save to file
File.WriteAllBytes("output.jp2", encoded);
```

### Adding EXIF Data via UUID Box

```csharp
var metadata = new J2KMetadata();

// Add EXIF data (you would generate proper EXIF binary format)
byte[] exifData = CreateExifData(); // Your EXIF creation logic
metadata.AddUuid(UuidBox.ExifUuid, exifData);

var encoded = J2kImage.ToBytes(imageSource, metadata);
```

## Metadata Classes

### `J2KMetadata`

Main container for all metadata types.

**Properties:**
- `Comments` - List of text comments
- `XmlBoxes` - List of XML boxes (XMP, IPTC, etc.)
- `UuidBoxes` - List of UUID boxes with custom data

**Methods:**
- `AddComment(text, language)` - Add a text comment
- `AddXml(xmlContent)` - Add an XML box
- `AddUuid(uuid, data)` - Add a UUID box
- `GetXmp()` - Get first XMP metadata box
- `GetIptc()` - Get first IPTC metadata box

### `XmlBox`

Represents an XML metadata box.

**Properties:**
- `XmlContent` - The XML content as string
- `IsXMP` - True if this appears to be XMP metadata
- `IsIPTC` - True if this appears to be IPTC metadata

### `UuidBox`

Represents a UUID box with vendor-specific data.

**Properties:**
- `Uuid` - The UUID identifying the data format
- `Data` - Binary payload data
- `IsXmp` - True if this is a known XMP UUID
- `IsExif` - True if this is a known EXIF UUID

**Static Fields:**
- `XmpUuid` - Well-known UUID for XMP (be7acfcb-97a9-42e8-9c71-999491e3afac)
- `ExifUuid` - Well-known UUID for EXIF (4a504720-0d0a-870a-0000-000000000000)

**Methods:**
- `GetTextData()` - Attempts to decode data as UTF-8 text

### `CommentBox`

Represents a text comment.

**Properties:**
- `Text` - The comment text
- `Language` - ISO 639 language code (default: "en")
- `IsBinary` - True if contains binary data (not UTF-8 text)

## Technical Details

### Supported Box Types

| Box Type | FourCC | Description |
|----------|--------|-------------|
| XML Box | `xml ` (0x786D6C20) | Structured XML metadata |
| UUID Box | `uuid` (0x75756964) | Vendor-specific binary data |

### File Format Compliance

- Conforms to **ISO/IEC 15444-1** (JPEG2000 Part 1)
- XML boxes use UTF-8 encoding as per spec
- UUID boxes follow standard 16-byte UUID + data format
- Metadata boxes are written between JP2 Header and Codestream boxes

### Limitations

- COM (comment marker segment) within codestream not yet supported
- IPTC-IIM (non-XML IPTC) not yet supported  
- Label boxes (Part 2) not yet supported
- Association boxes not yet supported

## Migration from libKDU

CoreJ2K metadata support is similar to libKDU's approach:

| libKDU | CoreJ2K |
|--------|---------|
| `kdu_codestream::access_comments()` | `J2KMetadata.Comments` |
| XML boxes via `jp2_input_box` | `J2KMetadata.XmlBoxes` |
| UUID boxes via `jp2_input_box` | `J2KMetadata.UuidBoxes` |
| Writing via `jp2_target` | `FileFormatWriter.Metadata` |

## Examples in Tests

See `tests/CoreJ2K.Tests/MetadataTests.cs` for comprehensive examples including:
- Creating and accessing metadata
- XMP and IPTC detection
- UUID box handling
- Complete encode/decode workflow with metadata

## Future Enhancements

Potential additions for future versions:
- COM marker segment support (codestream comments)
- IPTC-IIM binary format support
- Association boxes for linking related data
- Label boxes for human-readable identifiers
- Automatic metadata validation
- Helper methods for common XMP operations

## License

Copyright (c) 2025 Sjofn LLC  
Licensed under the BSD 3-Clause License
