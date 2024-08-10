# SpawnDev.EBML

| Name | Package | Description |
|---------|-------------|-------------|
|**SpawnDev.EBML**|[![NuGet version](https://badge.fury.io/nu/SpawnDev.EBML.svg)](https://www.nuget.org/packages/SpawnDev.EBML)| An extendable .Net library for reading and writing Extensible Binary Meta Language (aka EBML) documents. Includes schema for Matroska and WebM. | 

The demo project, EBMLViewer, included in the project is a .Net 8 Forms app for testing the library and viewing EBML documents.

## EBMLDocumentReader

EBMLDocumentReader is the base EBML document parser. The EBMLDocumentReader can be given a list of EBMLSchemas that tell it how to process EBML documents with a matching EBML.DocType value. WebM and Matroska EBMLSchemas are included in the library as WebMSchema and MatroskaSchema.

## WebMDocumentReader


To fix the duration in a WebM file, WebM parser reads the Timecode information from Clusters and SimpleBlocks and adds a Segment > Info > Duration element with the new duration.

Example of how to add Duration info if not found in a the WebM stream.
```cs
using var inputStream = new FileStream(inputFile, FileMode.Open, FileAccess.Read, FileShare.Read);

var webm = new WebMDocumentReader(inputStream);

// FixDuration returns true if the WebM was modified
var modified = webm.FixDuration();
// webm.DataChanged will also be true if the WebM is modified
if (modified)
{
    var outFile = Path.Combine(Path.GetDirectoryName(inputFile)!, Path.GetFileNameWithoutExtension(inputFile) + ".fixed" + Path.GetExtension(inputFile));
    using var outputStream = new FileStream(outFile, FileMode.Create, FileAccess.Write, FileShare.None);
    webm.CopyTo(outputStream);
}
```

Example of how to get an element
```cs
var durationElement = webm.GetElement<FloatElement>(MatroskaId.Segment, MatroskaId.Info, MatroskaId.Duration);
var duration = durationElement?.Data ?? 0;
```

Example of how to get all elements of a type
```cs
var segments = webm.GetElements<ContainerElement>(MatroskaId.Segment);
```

Example of how to use ElementIds to walk the data tree and access information
```cs
var segments = webm.GetContainers(MatroskaId.Segment);
foreach (var segment in segments)
{
    var clusters = segment.GetContainers(MatroskaId.Cluster);
    foreach (var cluster in clusters)
    {
        var timecode = cluster.GetElement<UintElement>(MatroskaId.Timecode);
        if (timecode != null)
        {
            duration = timecode.Data;
        };
        var simpleBlocks = cluster.GetElements<SimpleBlockElement>(MatroskaId.SimpleBlock);
        var simpleBlockLast = simpleBlocks.LastOrDefault();
        if (simpleBlockLast != null)
        {
            duration += simpleBlockLast.Timecode;
        }
    }
}
```

Example of how to add an element  
All parent containers are automatically marked Modified if any children are added, removed, or changed.
```cs
var info = GetContainer(MatroskaId.Segment, MatroskaId.Info);
info!.Add(MatroskaId.Duration, 100000);
```

# EBMLViewer
EBMLViewer is a demo app for the library. Supports WebM, Matroska, and other EBML document types.

![](https://raw.githubusercontent.com/LostBeard/SpawnDev.EBML/main/EBMLViewer/Images/Screenshot_3.jpg)

