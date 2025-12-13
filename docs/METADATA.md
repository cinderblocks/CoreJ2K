# JPEG2000 Metadata Support in CoreJ2K

CoreJ2K now supports reading and writing metadata (XML boxes, UUID boxes, comments, and **ICC color profiles**) in JPEG2000 files, conforming to ISO/IEC 15444-1 (JPEG2000 Part 1).

## Features

- ? **XML Boxes** - Store structured metadata (XMP, IPTC, etc.)
- ? **UUID Boxes** - Store vendor-specific binary data
- ? **Comment Support** - Text comments with language codes
- ? **ICC Color Profiles** - Embed and extract ICC profiles for accurate color representation
- ? **XMP Detection** - Automatic identification of XMP metadata
- ? **IPTC Detection** - Automatic identification of IPTC metadata
- ? **Well-Known UUIDs** - Support for standard UUID identifiers (XMP, EXIF)

## Usage Examples

### Reading Metadata and ICC Profile from a JPEG2000 File

```csharp
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;
using System.IO;

// Decode image and extract metadata including ICC profile
using (var stream = File.OpenRead("image.jp2"))
{
    var image = J2kImage.FromStream(stream, out J2KMetadata metadata);
    
    // Access ICC color profile
    if (metadata.IccProfile != null && metadata.IccProfile.IsValid)
    {
        Console.WriteLine($"ICC Profile found: {metadata.IccProfile}");
        Console.WriteLine($"Color Space: {metadata.IccProfile.ColorSpaceType}");
        Console.WriteLine($"Profile Class: {metadata.IccProfile.ProfileClass}");
        Console.WriteLine($"Profile Version: {metadata.IccProfile.ProfileVersion}");
        Console.WriteLine($"Profile Size: {metadata.IccProfile.ProfileSize} bytes");
        
        // Save ICC profile to file
        File.WriteAllBytes("extracted_profile.icc", metadata.IccProfile.ProfileBytes);
    }
    
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
}
```

### Writing Metadata with ICC Profile to a JPEG2000 File

```csharp
using CoreJ2K;
using CoreJ2K.j2k.fileformat.metadata;
using CoreJ2K.j2k.image;
using System.IO;

// Create metadata
var metadata = new J2KMetadata();

// Add a simple comment
metadata.AddComment("Created with CoreJ2K", "en");

// Add ICC color profile from file
var iccProfileBytes = File.ReadAllBytes("AdobeRGB1998.icc");
metadata.SetIccProfile(iccProfileBytes);

// Verify profile was loaded
if (metadata.IccProfile.IsValid)
{
    Console.WriteLine($"ICC Profile loaded: {metadata.IccProfile}");
}

// Add XMP metadata
var xmpXml = @"<?xml version=""1.0""?>
<x:xmpmeta xmlns:x=""adobe:ns:meta/"">
  <rdf:RDF xmlns:rdf=""http://www.w3.org/1999/02/22-rdf-syntax-ns#"">
    <rdf:Description rdf:about="""" xmlns:dc=""http://purl.org/dc/elements/1.1/"">
      <dc:title>My Image Title</dc:title>
      <dc:creator>John Doe</dc:creator>
    </rdf:Description>
  </rdf:RDF>
</x:xmpmeta>";
metadata.AddXml(xmpXml);

// Encode image with metadata and ICC profile
BlkImgDataSrc imageSource = ...; // your image source
var encoded = J2kImage.ToBytes(imageSource, metadata);

// Save to file
File.WriteAllBytes("output_with_profile.jp2", encoded);
```

### Creating an ICC Profile from Scratch (Advanced)

```csharp
// For simple cases, you typically load an existing ICC profile
var srgbProfile = File.ReadAllBytes("sRGB_IEC61966-2-1.icc");
metadata.SetIccProfile(srgbProfile);

// The ICCProfileData class validates the profile
if (metadata.IccProfile.IsValid)
{
    Console.WriteLine("Profile is valid ICC format");
}
else
{
    Console.WriteLine("Invalid ICC profile");
}
```

### Checking ICC Profile Color Space

```csharp
using CoreJ2K.Color.ICC;

var metadata = new J2KMetadata();
metadata.SetIccProfile(profileBytes);

if (metadata.IccProfile != null)
{
    // Check color space using predefined constants
    switch (metadata.IccProfile.ColorSpaceType)
    {
        case ICCProfileData.ColorSpaces.RGB:
            Console.WriteLine("RGB color space");
            break;
        case ICCProfileData.ColorSpaces.Gray:
            Console.WriteLine("Grayscale color space");
            break;
        case ICCProfileData.ColorSpaces.CMYK:
            Console.WriteLine("CMYK color space");
            break;
        default:
            Console.WriteLine($"Other color space: {metadata.IccProfile.ColorSpaceType}");
            break;
    }
    
    // Check profile class
    switch (metadata.IccProfile.ProfileClass)
    {
        case ICCProfileData.ProfileClasses.Display:
            Console.WriteLine("Display profile (monitor)");
            break;
        case ICCProfileData.ProfileClasses.Input:
            Console.WriteLine("Input profile (scanner/camera)");
            break;
        case ICCProfileData.ProfileClasses.Output:
            Console.WriteLine("Output profile (printer)");
            break;
    }
}
```

## Metadata Classes

### `J2KMetadata`

Main container for all metadata types.

**Properties:**
- `Comments` - List of text comments
- `XmlBoxes` - List of XML boxes (XMP, IPTC, etc.)
- `UuidBoxes` - List of UUID boxes with custom data
- `IccProfile` - ICC color profile data

**Methods:**
- `AddComment(text, language)` - Add a text comment
- `AddXml(xmlContent)` - Add an XML box
- `AddUuid(uuid, data)` - Add a UUID box
- `SetIccProfile(profileBytes)` - Set ICC color profile
- `GetXmp()` - Get first XMP metadata box
- `GetIptc()` - Get first IPTC metadata box

### `ICCProfileData`

Represents an ICC color profile.

**Properties:**
- `ProfileBytes` - Raw ICC profile bytes
- `ProfileSize` - Size in bytes
- `ProfileVersion` - ICC profile version (e.g., v2.1, v4.0)
- `ColorSpaceType` - Color space signature (RGB, CMYK, GRAY, etc.)
- `ProfileClass` - Profile/device class (mntr, scnr, prtr, etc.)
- `IsValid` - Whether the profile is a valid ICC profile

**Color Space Constants:**
- `ColorSpaces.RGB` - RGB color space
- `ColorSpaces.Gray` - Grayscale
- `ColorSpaces.CMYK` - CMYK
- `ColorSpaces.Lab` - CIE L*a*b*
- `ColorSpaces.XYZ` - CIE XYZ

**Profile Class Constants:**
- `ProfileClasses.Display` - Display/monitor profile
- `ProfileClasses.Input` - Input device (scanner/camera)
- `ProfileClasses.Output` - Output device (printer)
- `ProfileClasses.Link` - Device link profile
- `ProfileClasses.ColorSpace` - Color space conversion

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
| Colour Specification | `colr` (0x636F6C72) | Color space and ICC profile |
| XML Box | `xml ` (0x786D6C20) | Structured XML metadata |
| UUID Box | `uuid` (0x75756964) | Vendor-specific binary data |

### ICC Profile Support

CoreJ2K supports **Method 2** (ICC profiled) color specification boxes as defined in ISO/IEC 15444-1:

- **Reading**: Automatically extracts ICC profiles from `colr` boxes in JP2 Header
- **Writing**: Embeds ICC profiles when `Metadata.IccProfile` is set
- **Validation**: Verifies ICC profile header structure and size
- **Color Spaces**: Supports RGB, CMYK, Grayscale, Lab, XYZ, and more
- **Profile Classes**: Display, Input, Output, Link, Color Space, Abstract

### File Format Compliance

- Conforms to **ISO/IEC 15444-1** (JPEG2000 Part 1)
- Color Specification box with METH=2 for ICC profiles
- XML boxes use UTF-8 encoding as per spec
- UUID boxes follow standard 16-byte UUID + data format
- Metadata boxes are written between JP2 Header and Codestream boxes

### ICC Profile Specifications

- Minimum profile size: 128 bytes
- Supported ICC versions: 2.x, 4.x
- Profile validation includes:
  - Size verification
  - Header structure check
  - Version extraction
  - Color space identification
  - Profile class identification

### Limitations

- COM (comment marker segment) within codestream not yet supported
- IPTC-IIM (non-XML IPTC) not yet supported
- Label boxes (Part 2) not yet supported
- Association boxes not yet supported
- ICC profile application/color transformation not implemented (profiles are stored/retrieved only)

## Common ICC Profiles

Here are some commonly used ICC profiles you might embed:

| Profile | Color Space | Use Case |
|---------|-------------|----------|
| sRGB IEC61966-2.1 | RGB | Web, general display |
| Adobe RGB (1998) | RGB | Photography, print |
| ProPhoto RGB | RGB | Wide gamut photography |
| Display P3 | RGB | Modern displays |
| Gray Gamma 2.2 | Grayscale | Grayscale images |
| SWOP CMYK | CMYK | Print production |

**Note**: CoreJ2K does not include ICC profiles. You must provide your own profile files, which can be obtained from:
- Operating system (Windows: `C:\Windows\System32\spool\drivers\color\`)
- ICC.org
- Color profile vendors

## Migration from libKDU

CoreJ2K ICC profile support is similar to libKDU's approach:

| libKDU | CoreJ2K |
|--------|---------|
| `jp2_colour::get_icc_profile()` | `J2KMetadata.IccProfile.ProfileBytes` |
| `jp2_colour::set_icc_profile()` | `J2KMetadata.SetIccProfile()` |
| `jp2_colour::get_space()` | `J2KMetadata.IccProfile.ColorSpaceType` |
| Profile validation | `ICCProfileData.IsValid` |

## Examples in Tests

See `tests/CoreJ2K.Tests/ICCProfileTests.cs` for comprehensive examples including:
- Creating minimal ICC profiles for testing
- Validating ICC profile structure
- Color space detection
- Profile class identification
- Defensive copying verification

## Future Enhancements

Potential additions for future versions:
- ICC profile application (color transformation)
- Built-in common profiles (sRGB, Adobe RGB)
- Profile generation utilities
- Color space conversion using profiles
- Profile embedding optimization
- V4 ICC profile features
- Restricted ICC profile support (METH=1 with restricted ICC)

## License

Copyright (c) 2025 Sjofn LLC  
Licensed under the BSD 3-Clause License
