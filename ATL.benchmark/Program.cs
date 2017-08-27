using BenchmarkDotNet.Running;
using System;
using System.IO;

namespace ATL.benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkRunner.Run<Speed>();

            //BenchmarkRunner.Run<ATLOld_ATLNew>();

            //BenchmarkRunner.Run<ATL_TagLib>();

            readAt("E:/temp/id3v2", true);
        }

        static private void readAt(string filePath, bool useOldImplementation = false)
        {
            FileFinder ff = new FileFinder();

            Console.WriteLine(filePath);

            if (File.Exists(filePath))
            {
                Track t = new Track(filePath, useOldImplementation);
                t.GetEmbeddedPicture(useOldImplementation);

                Console.WriteLine(t.Title);
            }
            else if (Directory.Exists(filePath))
            {
                ff.FF_BrowseATLAudioFiles(filePath, false, true, true);
                Console.WriteLine("________________________________________________________");
                ff.FF_BrowseTagLibAudioFiles(filePath, true, true);
            }

            Console.WriteLine("end");
            Console.ReadLine();
        }
    }
}
