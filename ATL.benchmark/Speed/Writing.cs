using ATL.AudioData;
using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System.IO.MemoryMappedFiles;

namespace ATL.benchmark
{
    public class Writing
    {
        //static string LOCATION = TestUtils.GetResourceLocationRoot()+"MP3/01 - Title Screen_pic.mp3";
        [Params(@"FLAC/flac.flac")]
        public string initialFileLocation;

        private IList<string> tempFiles = new List<string>();


        [GlobalSetup]
        public void Setup(string fileName = "")
        {
            tempFiles.Clear();
            // Duplicate resource
            tempFiles.Add(TestUtils.GenerateTempTestFile(fileName.Length > 0 ? fileName : initialFileLocation));
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Mass delete resulting files
            foreach (string s in tempFiles)
            {
                File.Delete(s);
            }

            tempFiles.Clear();
        }

        [Benchmark(Baseline = true)]
        public void Perf_WriteFLAC()
        {
            performWrite();
        }

        /*
        [Benchmark]
        public void Perf_WriteFLACMemory()
        {
            AudioDataManager.SetFileOptions(FileOptions.None);
            AudioDataManager.SetBufferSize(4096);

            performWrite();
        }
        */

        private void performWrite()
        {
            // Mass-read resulting files
            foreach (string s in tempFiles)
            {
                Track t = new Track(s);

                t.AdditionalFields.Add(new KeyValuePair<string, string>("test","aaa"));

                t.Save();
            }
        }
    }
}
