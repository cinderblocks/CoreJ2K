# Metadata Configuration API Guide

## Overview

`MetadataConfigurationBuilder` configures metadata embedded in JP2/JPX files:
human-readable comments, structured XML, vendor UUID boxes, and intellectual
property rights text. It is used standalone or via `CompleteEncoderConfigurationBuilder`.

## Quick Start

```csharp
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForLossless()
    .WithEncoder(e => e.WithFileFormat(true))
    .WithMetadata(m => m
        .WithCopyright("© 2026 Acme Corp")
        .WithComment("Scanned at 600 dpi"))
    .Encode(imageSource);
```

## Comments

Text comments are stored in `COM` marker segments in the codestream. Multiple
comments are allowed.

```csharp
var m = new MetadataConfigurationBuilder()
    .WithComment("Created by CoreJ2K")
    .WithComment("Internal reference: IMG-001");

// Or add several at once
m.WithComments("Title: Specimen A", "Date: 2026-06-12", "Operator: jdoe");
```

## Copyright / Intellectual Property Rights

IPR text is stored in the JP2 `ipr ` box (ISO/IEC 15444-1 §I.7.3).

```csharp
var m = new MetadataConfigurationBuilder()
    .WithCopyright("© 2026 Acme Corp. All rights reserved.");
// WithCopyright is an alias for WithIntellectualPropertyRights.
```

## XML Metadata

XML blocks are stored in `xml ` boxes. Multiple XML blocks are allowed.

```csharp
var xmp = """
    <x:xmpmeta xmlns:x="adobe:ns:meta/">
      <rdf:RDF xmlns:rdf="http://www.w3.org/1999/02/22-rdf-syntax-ns#">
        <rdf:Description rdf:about="" xmlns:dc="http://purl.org/dc/elements/1.1/">
          <dc:title>Sample Image</dc:title>
        </rdf:Description>
      </rdf:RDF>
    </x:xmpmeta>
    """;

var m = new MetadataConfigurationBuilder().WithXml(xmp);
```

## UUID Data

UUID boxes store vendor-specific or application-specific binary data.

```csharp
// Binary data
var uuid = new Guid("12345678-1234-1234-1234-123456789abc");
byte[] exifData = File.ReadAllBytes("metadata.exif");
var m = new MetadataConfigurationBuilder().WithUuid(uuid, exifData);

// String data (UTF-8 encoded automatically)
m.WithUuid(uuid, "application-specific payload");
```

## Clearing Metadata

```csharp
m.ClearComments();  // remove all comments
m.ClearXml();       // remove all XML blocks
m.ClearUuids();     // remove all UUID boxes
m.ClearAll();       // remove everything
```

## Presets

`MetadataPresets` provides factory helpers:

```csharp
var m = MetadataPresets.WithCopyright("© 2026 Acme");
var m = MetadataPresets.WithTitleAndDescription("Sample", "A test image");
var m = MetadataPresets.WithExif(exifXml);
```

## Using with CompleteEncoderConfigurationBuilder

```csharp
var bytes = new CompleteEncoderConfigurationBuilder()
    .ForArchival()
    .WithEncoder(e => e.WithFileFormat(true))
    .WithMetadata(m => m
        .WithCopyright("© 2026 Archive Inc.")
        .WithXml(xmpBlock)
        .WithComment("Digitised 2026-06-12"))
    .Encode(imageSource);
```

Convenience shortcuts on the builder forward to `MetadataConfigurationBuilder`:

```csharp
builder.WithComment("text");          // same as WithMetadata(m => m.WithComment(...))
builder.WithCopyright("© 2026 ...");  // same as WithMetadata(m => m.WithCopyright(...))
```

## Standalone Usage

```csharp
var metaBuilder = new MetadataConfigurationBuilder()
    .WithComment("Standalone usage example")
    .WithCopyright("© 2026 Corp");

var metadata = metaBuilder.ToJ2KMetadata();

var pl = J2kImage.GetDefaultEncoderParameterList();
pl["file_format"] = "on";
byte[] data = J2kImage.ToBytes(imageSource, metadata, pl);
```

## Reading Metadata Back

```csharp
var image = J2kImage.FromStream(stream, out var metadata, parameters);

foreach (var comment in metadata.Comments)
    Console.WriteLine(comment);

if (metadata.IntellectualPropertyRights != null)
    Console.WriteLine(metadata.IntellectualPropertyRights);
```

## Validation

```csharp
var m = new MetadataConfigurationBuilder().WithComment("ok");
if (!m.IsValid)
    Console.WriteLine(string.Join(", ", m.Validate()));
```
