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

            //BenchmarkRunner.Run<Misc>();

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\OGG\ogg.ogg");

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\OGG\ogg.ogg");

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\MP3\id3v2.4_UTF8.mp3");

            //readAt(@"E:\Dev\Source\Repos\atldotnet\ATL.test\Resources\MP3\id3v2.4_UTF8.mp3");


            //readAt(@"E:\Music\Anime\Pita Ten\OST 1\02 - Kotarou, itsumo no mainichi.mp3");

            compareInfo(@"E:\Music\Anime");
        }

        static private void readAt(string filePath, bool useOldImplementation = false, bool useTagLib = false)
        {
            FileFinder ff = new FileFinder();

//            Console.WriteLine(filePath);

            if (File.Exists(filePath))
            {
                Track t = new Track(filePath, useOldImplementation);
                //t.GetEmbeddedPicture(useOldImplementation, false);

//                Console.WriteLine(t.Title);
            }
            else if (Directory.Exists(filePath))
            {
                ff.FF_BrowseATLAudioFiles(filePath, false, true, true);
                if (useTagLib)
                {
                    Console.WriteLine("________________________________________________________");
                    ff.FF_BrowseTagLibAudioFiles(filePath, true, true);
                }
            }

//            Console.WriteLine("end");
//            Console.ReadLine();
        }

        static private void compareInfo(string path)
        {
            FileFinder_ATL_NewOld ff = new FileFinder_ATL_NewOld();

            ff.FF_RecursiveExplore(path);
        }
    }
}
