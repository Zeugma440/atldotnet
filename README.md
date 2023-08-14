# Audio Tools Library (ATL) for .NET ![NetCore](https://img.shields.io/badge/.NET%20Core-3.1-lightgrey.svg) ![NetStandard](https://img.shields.io/badge/.NET%20Standard-2.1-lightgrey.svg) ![.NET](https://img.shields.io/badge/.NET-6.0-lightgrey.svg)

__Latest stable version__ : [![NuGet](https://img.shields.io/nuget/v/z440.atl.core.svg)](https://www.nuget.org/packages/z440.atl.core/)

__Optimized with__ : [BenchmarkDotNet](https://github.com/dotnet/BenchmarkDotNet) and [CodeTrack](http://www.getcodetrack.com/)

## Current status

[![.NET](https://github.com/Zeugma440/atldotnet/actions/workflows/dotnet.yml/badge.svg?branch=main)](https://github.com/Zeugma440/atldotnet/actions/workflows/dotnet.yml) [![codecov](https://codecov.io/gh/Zeugma440/atldotnet/branch/main/graph/badge.svg)](https://codecov.io/gh/Zeugma440/atldotnet) [![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Zeugma440_atldotnet&metric=alert_status)](https://sonarcloud.io/dashboard?id=Zeugma440_atldotnet)

## What is ATL .NET ?

This library is aimed at giving .NET developers a managed, portable and easy-to-use library to **read and write metadata from digital audio files and playlists** with one single unified API, whatever the underlying format.

```csharp
using ATL.AudioData;

// Initialize with a file path
Track theTrack = new Track(audioFilePath);

// Works the same way on any supported format (MP3, FLAC, WMA, SPC...)
System.Console.WriteLine("Title : " + theTrack.Title);
System.Console.WriteLine("Duration (ms) : " + theTrack.DurationMs);

theTrack.Composer = "Oscar Wilde (アイドル)"; // Support for "exotic" charsets
theTrack.AdditionalFields["customField"] = "fancyValue"; // Support for custom fields
theTrack.Save();

// Can alternatively be initialized with a Stream
theTrack = new Track(stream);
```

You'll find more working code on the [Code snippets section of the Documentation](https://github.com/Zeugma440/atldotnet/wiki/3.-Usage-_-Code-snippets), including what you need to manage embedded pictures (e.g. cover), chapters , lyrics and playlists


## What is NOT ATL .NET ?

Audio Tools Library .NET is not

* a standalone application : it is a library aimed at being used by developers to build software

* an audio music player : it gives access to various properties and metadata (see below for the comprehensive list), but does not process audio data into an audible signal


## Why open source ?

ATL has been open source since its creation. The original ATL 2.3 source written in Pascal language is still out there on Sourceforge !

By publicly sharing the result of their work, the MAC team has helped many developers to gain tremendous time in creating audio tools.

As a fellow audiophile and developer, I'm proudly extending and improving their initial contribution to the open source community.


## Why would I want to use ATL while TagLib is out there ?

* ATL has a __full C# implementation__ and does not use any dependency, which makes portability trivial if your app is already based on .NET or Mono frameworks

* ATL can __auto-detect the format__ of the audio data you're using, even if the file extension has the wrong label or if you're feeding it raw data witout context

* ATL features a __flexible logging system__ which allows you to catch and record audio file reading/writing incidents into your app

* ATL supports __more audio formats than TagLib, including video game audio formats (SPC, PSF, VGM, GYM)__

* ATL supports __[chapters](https://github.com/Zeugma440/atldotnet/wiki/Focus-on-Chapter-metadata)__ natively

* ATL supports __lyrics__ natively

* ATL supports BEXT, LIST (general metadata, labels and notes), CUE (cue points), SMPL (sample loops), DISP (displayable objects) and iXML metadata chunks in RIFF / WAV files

* ATL supports __Playlists and Cuesheets__

* ATL supports [async calls](https://github.com/Zeugma440/atldotnet/wiki/Progress-feedback) on writing operations


## How to use it ?  Which platforms and .NET/Mono versions does ATL run on ?

The ATL library runs on .NET Core 3.1+ / .NET Standard 2.1+ / .NET Framework 4.8+

ATL unit tests run on .NET Framework 4.8+

The library and its tests have been maintained on Visual Studio Express and Community (currently on Community 2022)

Please refer to the [Code snippets section of the Documentation](https://github.com/Zeugma440/atldotnet/wiki/3.-Usage-_-Code-snippets) for quick usage


## What kind of data can ATL actually read ? From which formats ?

### SUPPORTED AUDIO FORMATS AND TAGGING STANDARDS

NB1 : Empty cells mean "not applicable for this audio format"

NB2 : All metadata is read according to Unicode/UTF-8 encoding when applicable, which means any "foreign" character (japanese, chinese, cyrillic...) __will__ be recognized and displayed properly

R= Read / W= Write


Audio format | Extensions | ID3v1.0-1.1 support | ID3v2.2-2.4 support (1) | APEtag 1.0-2.0 support | Format-specific tagging support
---|---|---|---|---|---
Advanced Audio Coding, Apple Lossless (ALAC) | .AAC, .MP4, .M4A, .M4B | R/W | R/W | R/W | R/W
Apple Core Audio | .CAF |  |  |  | (5)
Audible | .AAX, .AA | R/W | R/W | R/W | R/W
Audio Interchange File Format | .AIF, .AIFF, .AIFC |  | R/W |  | R/W
Digital Theatre System | .DTS |  |  |  | 
Direct Stream Digital | .DSD, .DSF |  | R/W |  | 
Dolby Digital | .AC3 |  |  | R/W | 
Extended Module | .XM |  |  |  | R/W (2)
Free Lossless Audio Codec | .FLAC |  | R/W |  | R/W
Genesis YM2612 | .GYM |  |  |  | R/W
Impulse Tracker | .IT |  |  |  | R/W (2)
Musical Instruments Digital Interface | .MID, .MIDI |  |  |  | R/W (3)
Monkey's Audio | .APE | R/W | R/W | R/W | 
MPEG Audio Layer | .MP1, .MP2, .MP3 | R/W | R/W | R/W | |
MusePack / MPEGplus|.MPC, .MP+|R/W|R/W|R/W| |
Noisetracker/Soundtracker/Protracker|.MOD| | | |R/W (2)|
OGG : Vorbis, Opus, Embedded FLAC|.OGG, .OGA, .OPUS| | | |R/W|
OptimFROG|.OFR, .OFS|R/W|R/W|R/W| |
Portable  Sound Format|.PSF, .PSF1, .PSF2, .MINIPSF, .MINIPSF1, .MINIPSF2, .SSF, .MINISSF, .DSF, .MINIDSF, .GSF, .MINIGSF, .QSF, .MINIQSF| | | |R/W|
ScreamTracker|.S3M| | | |R/W (2)|
SPC700 (Super Nintendo Sound files)|.SPC| | | |R/W|
Toms' losslesss Audio Kompressor|.TAK| | |R/W| |
True Audio|.TTA|R/W|R/W|R/W| |
TwinVQ|.VQF| | | |R/W|
PCM (uncompressed audio)|.WAV, .BWAV, .BWF|R/W|R/W| |R/W (4)|
Video Game Music (SEGA systems sound files) | .VGM, .VGZ |  |  |  | R/W
WavPack|.WV| | |R/W| |
Windows Media Audio/Advanced Systems Format|.WMA, .ASF| | | |R/W|


(1) : ATL reads ID3v2.2, ID3v2.3 and ID3v2.4 tags, but only writes ID3v2.3 tags and ID3v2.4 tags

(2) : all sample names appear on the track's Comment field. Track title only is editable

(3) : MIDI meta events appear on the track's Comment field

(4) : Support for BEXT, LIST (general metadata, labels and notes), CUE (cue points), SMPL (sample loops), DISP (displayable objects) and iXML metadata chunks

(5) : Reads audio properties only, due to the rarity of sample CAF files tagged with actual metadata


### DETECTED FIELDS

* __Audio properties (from audio data)__ : Bitrate, Bit depth (bits per sample), Sample rate, Duration, VBR, Codec family, Channels count and arrangement
* __Standard Metadata (from tags)__ : Title, Artist, Album Artist, Composer, Conductor, Description, Comment, Genre, Track number, Total tracks, Disc number, Total discs, Recording Year and Date, Album, Rating, Publisher, Publishing Date, Copyright, Original album, Original artist, Album sort order, Album artist sort order, Artist sort order, Title sort order, Content group description, Series title / Movement name, Series part / Movement index, Long description (also known as "Podcast description"), Embedded pictures, [Chapters](https://github.com/Zeugma440/atldotnet/wiki/Focus-on-Chapter-metadata), Unsynchronized and synchronized Lyrics
* __Custom Metadata__ : any other field that might be in the tag is readable __and__ editable by ATL

NB : Most metadata will be read "as is", with minimal alteration. If your file has invalid tags (e.g. chapter start timecode located after file duration), ATL will give you back that information without trying to auto-correct it.


### SUPPORTED PLAYLISTS FORMATS

* Read and write : ASX, B4S, M3U, M3U8, PLS, SMIL (including WPL and ZPL), XSPF, DPL (PotPlayer playlist)
* Read-only : FPL (Foobar2000 playlist)

See detailed compatibility table [here](https://docs.google.com/spreadsheets/d/1Wo9ifsKbBloofdWCsoXziAtaS-QVjqci5aavAV8dt2U/edit?usp=sharing)


### SUPPORTED CUESHEETS FORMATS

CUE


## What is the roadmap of ATL.NET ?

* Support for Broadcast wave metadata : aXML and XMP
* Support for other audio file formats : Speex
* Connectors to __other library file formats__ (e.g. iTunes)

NB : Any user request that can be granted quickly will take priority over the roadmap


## Does ATL.NET include code authored by other people ?

ATL.NET is based on :

* Audio Tools Library 2.3  by Jurgen Faul, Mattias Dahlberg, Gambit, MaDah and Erik Stenborg (code ported from Pascal to C# and refactored)

* MIDI class 1.5 by Valentin Schmidt & Michael Mlivoncic (code ported from PHP to C# and refactored)


## Special thanks for their contributions to...

[leglubert](https://github.com/leglubert), [tarrats](https://github.com/tarrats), [DividedSE](https://github.com/DividedSE), [audiamus](https://github.com/audiamus)


## Find this library useful? :heart:
Support it by joining __[stargazers](https://github.com/Zeugma440/atldotnet/stargazers)__ for this repository. :star:
