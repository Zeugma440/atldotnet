using BenchmarkDotNet.Running;
using Commons;
using System;
using System.Diagnostics.Metrics;
using System.IO;
using ATL.Playlist;

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

            //BenchmarkRunner.Run<AsyncSyncLegacy>();

            //compareInfo(@"E:\Music\VGM");

            //browseFor(@"E:\Music\", "*.mp3");

            //writeAt(@"D:\temp\wav\broadcastwave_bext_info.wav");

            //info(@"D:\temp\wav\74\empty_tagged_audacity.wav");

            //browseForMultithread(@"E:\temp\m4a-mp4\issue 70", "*.*", 4);

            //info(@"D:\temp\wav\185\Largo.WAV");

            test();

            //reduce(@"D:\temp\m4a-mp4\160\2tracks_TestFromABC-Orig.m4a");

            //displayVersionInfo();
        }

        static private void test()
        {

            var path1 = @"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.unit-test\Resources\tmp\chron.mid--13.12.37.357.mid";
            var path2 = @"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.unit-test\Resources\tmp\empty.mp3--13.12.37.312.mp3";
            var path3 = @"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.unit-test\Resources\tmp\mod.mod--15.31.12.041.mod";

            var playlistPath = @"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.unit-test\Resources\tmp\playlist.m3u--15.31.35.063.m3u";

            var playlist = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath, PlaylistFormat.LocationFormatting.FilePath, PlaylistFormat.FileEncoding.UTF8_NO_BOM);
            playlist.FilePaths.Clear();
            playlist.Save();

            // Add absolute path
            ATL.Settings.PlaylistWriteAbsolutePath = true;
            playlist.FilePaths.Add(path1);
            playlist.Save();

            Console.WriteLine("Step 1:");
            Console.WriteLine(File.ReadAllText(playlistPath));

            // Add relative path
            playlist = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath, PlaylistFormat.LocationFormatting.FilePath, PlaylistFormat.FileEncoding.UTF8_NO_BOM);
            ATL.Settings.PlaylistWriteAbsolutePath = false;
            playlist.FilePaths.Add(path2);
            playlist.Save();

            Console.WriteLine("\nStep 2:");
            Console.WriteLine(File.ReadAllText(playlistPath));

            // Add absolute path
            playlist = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath, PlaylistFormat.LocationFormatting.FilePath, PlaylistFormat.FileEncoding.UTF8_NO_BOM);
            ATL.Settings.PlaylistWriteAbsolutePath = true;
            playlist.FilePaths.Add(path3);
            playlist.Save();

            Console.WriteLine("\nStep 3:");
            Console.WriteLine(File.ReadAllText(playlistPath));
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

                Console.WriteLine(t.Title + " by " + t.Artist);
                if (t.EmbeddedPictures.Count > 0) Console.WriteLine(t.EmbeddedPictures.Count + " embedded pictures detected");
                if (t.Chapters.Count > 0) Console.WriteLine(t.Chapters.Count + " chapters detected");
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

            ff.FF_WriteAllInFolder(rootDir, filter, threads);

            Console.WriteLine(">>> BROWSE : END");
            Console.ReadLine();
        }

        static private void writeOnTmpResource(string fileName)
        {
            Writing w = new Writing();
            w.Setup(fileName);
            //w.Perf_Write();
            w.Cleanup();

            Console.ReadLine();
        }

        static private void writeAt(string filePath)
        {
            string testFileLocation = TestUtils.GenerateTempTestFile(filePath);
            try
            {
                //Settings.ForceDiskIO = true;
                Settings.FileBufferSize = 2 * 1024 * 1024;
                //Settings.FileBufferSize = 512;
                //                Settings.ID3v2_tagSubVersion = 3;

                //ConsoleLogger logger = new ConsoleLogger();
                Console.WriteLine(">>> WRITE : BEGIN @ " + testFileLocation);

                Writing w = new Writing();
                w.performWrite(testFileLocation);
                Console.WriteLine(">>> WRITE : END");

                Console.ReadLine();
            }
            finally
            {
                File.Delete(testFileLocation);
            }
        }

        static private void reduce(string filePath)
        {
            new Reduce().reduce(filePath);
        }

        static private void info(string filePath)
        {
            new ConsoleLogger();
            Console.WriteLine(">>> INFO : BEGIN @ " + filePath);

            Track t = new Track(filePath);

            Console.WriteLine(t.Path + "......." + t.AudioFormat.Name + " | " + Utils.EncodeTimecode_s(t.Duration) + " | " + t.SampleRate + " (" + t.Bitrate + " kpbs" + (t.IsVBR ? " VBR)" : ")" + " " + t.ChannelsArrangement));
            Console.WriteLine(Utils.BuildStrictLengthString("", t.Path.Length, '.') + ".......disc " + t.DiscNumber + " | track " + t.TrackNumber + " | title " + t.Title + " | artist " + t.Artist + " | album " + t.Album + " | year " + t.Year);

            Console.WriteLine("images : " + t.EmbeddedPictures.Count);

            Console.WriteLine(">>> INFO : END");

            Console.ReadLine();
        }

        static private void displayVersionInfo()
        {
            Console.WriteLine(ATL.Version.getVersion());
            Console.ReadLine();
        }
    }



}

