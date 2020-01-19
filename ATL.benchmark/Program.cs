using BenchmarkDotNet.Running;
using System;
using System.IO;

namespace ATL.benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkRunner.Run<BufferSizes>();

            //BenchmarkRunner.Run<ATLOld_ATLNew>();

            //BenchmarkRunner.Run<ATL_TagLib>();

            //BenchmarkRunner.Run<PictureReading>();

            //BenchmarkRunner.Run<Misc>();


            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\OGG\ogg.ogg");

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\MP3\id3v2.4_UTF8.mp3");

            //readAt(@"E:\temp\id3v2\Testband - Copy - Copy - Copy.mp3");


            //readAt(@"E:\temp\aac\mp4\chapters\multiTrack & chapters QT.m4a");
            //readAt(@"E:\temp\mp3\04+-+.mp3");

            //compareInfo(@"E:\Music\VGM");

            //browseFor(@"E:\Music\", "*.mp3");

            writeAt(@"FLAC/flac.flac");
        }

        static private void readAt(string filePath, bool useTagLib = false)
        {
            FileFinder ff = new FileFinder();
            ConsoleLogger log = new ConsoleLogger();

            //            Console.WriteLine(filePath);

            if (File.Exists(filePath))
            {
                Track t = new Track(filePath);
                //t.GetEmbeddedPicture(useOldImplementation, false);

                                Console.WriteLine(t.Title);
            }
            else if (Directory.Exists(filePath))
            {
                ff.FF_BrowseATLAudioFiles(filePath, true, true);
                if (useTagLib)
                {
                    Console.WriteLine("________________________________________________________");
                    ff.FF_BrowseTagLibAudioFiles(filePath, true, true);
                }
            }

            //            Console.WriteLine("end");
            Console.ReadLine();
        }

        static private void browseFor(string rootDir, string filter)
        {
            FileFinder ff = new FileFinder();

            ff.FF_RecursiveExplore(rootDir, filter);

            Console.WriteLine(">>> BROWSE : END");
            Console.ReadLine();
        }

        static private void writeAt(String fileName)
        {
            Writing w = new Writing();
            w.Setup(fileName);
            w.Perf_WriteFLAC();
            w.Cleanup();

            Console.ReadLine();
        }
    }
}
