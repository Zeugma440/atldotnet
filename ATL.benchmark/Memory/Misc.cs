using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Diagnostics.Windows.Configs;
using System.Collections;
using System.Collections.Generic;

namespace ATL.benchmark
{
    public class Misc
    {
        //[Params(64,128,512,1024,2048,4096,8192)]
        public int mode;

        public string path = TestUtils.GetResourceLocationRoot() + "OGG/ogg.ogg";
        //public string path = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.4_UTF8.mp3";
        

        FileFinder ff = new FileFinder();

        [Benchmark]
        public void mem_ATL()
        {
            //            BufferedBinaryReader.BUFFER_SIZE = mode;
            IList<TagData.PictureInfo> pictures;

            if (File.Exists(path))
            {
                Track t = new Track(path);
                pictures = t.EmbeddedPictures;
            }
            else if (Directory.Exists(path))
            {
                ff.FF_BrowseATLAudioFiles(path, true, false);
            }
        }
    }
}
