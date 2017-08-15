# Audio Tools Library (ATL) for .NET

## Status / IO branch

**Linux and OSX Mono Build :** [![Build Status](https://travis-ci.org/Zeugma440/atldotnet.svg?branch=IO)](https://travis-ci.org/Zeugma440/atldotnet) (powered by Travis CI)

**Windows .NET Core Build :** [![Build status](https://ci.appveyor.com/api/projects/status/s4y0e3g6fxncdhi6/branch/master?svg=true)](https://ci.appveyor.com/project/Zeugma440/atldotnet/branch/IO) (powered by AppVeyor)

**Code coverage :** [![codecov](https://codecov.io/gh/Zeugma440/atldotnet/branch/IO/graph/badge.svg)](https://codecov.io/gh/Zeugma440/atldotnet) (powered by CodeCov)


## What's on the IO branch ?

The IO branch is the testing grounds for a major overhaul of the library which includes :

  * Tag editing !
  * Access to all metadata, not only fields that are mapped to hardcoded getters / setters
  * More abstractions, less "dumb" code to be written to add a new format and/or a new metadata field
  * Performance improvements
  
It is currently being tested extensively, and will be merged with the main branch once all currently supported audio formats are covered.


**What's the progress so far ?**

* Tagging standards covered : ID3v1, ID3v2, APE

* Audio formats covered : MP3, MP4/AAC/M4A, WMA/ASF


**Will it require changes to change the way my program invoke ATL ?**

*AUDIO FILES*
  
Reading an audio file by using the Track class (see Usage / Code Snippets) won't change.
  
However, most classes under AudioReaders package will disappear in favor of the new AudioData package.


*PLAYLISTS & CUESHEETS*

Playlists and cuesheets classes won't change in the near future
