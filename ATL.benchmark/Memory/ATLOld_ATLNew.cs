using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;

namespace ATL.benchmark
{
    [MemoryDiagnoser]
    [InliningDiagnoser]
    public class ATLOld_ATLNew
    {
        //[Params("E:/temp/wma", "E:/temp/id3v2", "E:/temp/aac/mp4", "E:/temp/ogg", "E:/temp/flac")]
        [Params("E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/MP3/ID3v2.2 UTF16.mp3", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/MP3/id3v2.4_UTF8.mp3", "E:/Dev/Source/Repos/atldotnet/ATL.test/Resources/MP3/id3v2.3_UTF16.mp3")]
        public string path;

        FileFinder ff = new FileFinder();

        [Benchmark(Baseline = true)]
        public void mem_Old()
        {
            if (File.Exists(path))
            {
                Track t = new Track(path, true);
                t.GetEmbeddedPicture(true);
            } else if (Directory.Exists(path))
            {
                ff.FF_BrowseATLAudioFiles(path, true, true, false);
            }
        }

        [Benchmark]
        public void mem_New()
        {
            if (File.Exists(path))
            {
                Track t = new Track(path);
                t.GetEmbeddedPicture(false, false);
            }
            else if (Directory.Exists(path))
            {
                ff.FF_BrowseATLAudioFiles(path, false, true, false);
            }
        }
    }
}
