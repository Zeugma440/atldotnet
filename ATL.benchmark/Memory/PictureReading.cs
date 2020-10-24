using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using System.Collections.Generic;

namespace ATL.benchmark
{
    [MemoryDiagnoser]
    [InliningDiagnoser]
    public class PictureReading
    {
        [Params("E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/MP3/ID3v2.2 3 pictures.mp3", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/OGG/bigPicture.ogg", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/FLAC/flac.flac", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/WMA/wma.wma", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/APE/ape.ape", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/MP4/mp4.m4a")]
        public string path;

        FileFinder ff = new FileFinder();

        [Benchmark]
        public void mem_ATL()
        {
            if (File.Exists(path))
            {
                Track t = new Track(path);
                IList<PictureInfo> pictures = t.EmbeddedPictures;
            }
            else if (Directory.Exists(path))
            {
                ff.FF_BrowseATLAudioFiles(path, true, false);
            }
        }
    }
}
