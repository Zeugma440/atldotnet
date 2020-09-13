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

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\AAC\chapters_QT.m4v");

            //compareInfo(@"E:\Music\VGM");

            //browseFor(@"E:\Music\", "*.mp3");

            //writeAt(@"E:\temp\m4a-mp4\sample.original.edited - Copie.mp4");

            browseForMultithread(@"E:\temp\m4a-mp4", "*.*", 40);

            //readAt(@"E:\temp\m4a-mp4\sample.original - Copie.mp4");

            //readAt(@"E:\temp\wav\loop_points.wav");

            //displayVersionInfo();
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

        static private void browseForMultithread(string rootDir, string filter, int threads)
        {
            FileFinder ff = new FileFinder();

            ff.FF_RecursiveExplore(rootDir, filter, threads);

            Console.WriteLine(">>> BROWSE : END");
            Console.ReadLine();
        }

        static private void writeOnTmpResource(String fileName)
        {
            Writing w = new Writing();
            w.Setup(fileName);
            w.Perf_Write();
            w.Cleanup();

            Console.ReadLine();
        }

        static private void writeAt(String fileName)
        {
            ConsoleLogger logger = new ConsoleLogger();

            Writing w = new Writing();
            w.performWrite(fileName);
            Console.WriteLine(">>> WRITE : END");

            Console.ReadLine();
        }

        static private void displayVersionInfo()
        {
            Console.WriteLine(ATL.Version.getVersion());
            Console.ReadLine();
        }
    }

    

}
