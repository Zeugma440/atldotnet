using ATL.AudioData;
using ATL.Logging;
using System;
using System.Diagnostics;
using System.IO;

namespace ATL.benchmark
{
    //TODO - Test BenchmarkDotNet

    public class Memory
    {
        FileFinder ff = new FileFinder();

        public void Mem_OldNew()
        {
            ConsoleLogger log = new ConsoleLogger();

            //string path = TestUtils.GetResourceLocationRoot() + "MP3/id3v2.4_UTF8.mp3";
            string path = @"E:\temp\wma";

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            mem_Old(path);

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            mem_New(path);
        }

        private void mem_Old(string path)
        {
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "-------------------------------------------------------");
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "begin old");

            if (File.Exists(path))
            {
                Track t = new Track(path, true);
                t.GetEmbeddedPicture(true);
            } else if (Directory.Exists(path))
            {
                ff.FF_FilterAndDisplayAudioFiles(path, true);
            }

            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, GC.GetTotalMemory(false).ToString("N0"));
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, Process.GetCurrentProcess().WorkingSet64.ToString("N0"));
        }

        private void mem_New(string path)
        {
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "-------------------------------------------------------");
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "begin new");

            if (File.Exists(path))
            {
                Track t = new Track(path);
                t.GetEmbeddedPicture();
            }
            else if (Directory.Exists(path))
            {
                ff.FF_FilterAndDisplayAudioFiles(path);
            }

            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, GC.GetTotalMemory(false).ToString("N0"));
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, Process.GetCurrentProcess().WorkingSet64.ToString("N0"));
        }
    }
}
