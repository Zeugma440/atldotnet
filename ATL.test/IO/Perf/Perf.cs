using System;
using ATL.AudioData;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.IO;

namespace ATL.test.IO.Perf
{
    [TestClass]
    public class Perf
    {
        const int NB_COPIES = 2000;
        const FileOptions FILE_FLAG_NOBUFFERING = (FileOptions)0x20000000;

        static string LOCATION = TestUtils.GetResourceLocationRoot()+"01 - Title Screen_pic.mp3";

        private static string getNewLocation(int index)
        {
            return LOCATION.Replace("01", "tmp" + Path.DirectorySeparatorChar + index.ToString());
        }

        [TestMethod, TestCategory("mass")]
        public void Perf_Massread()
        {
            long ticksBefore, ticksNow;

            // Duplicate resource
            for (int i = 0; i < NB_COPIES; i++)
            {
                string newLocation = getNewLocation(i);
                File.Copy(LOCATION, newLocation, true);

                FileInfo fileInfo = new FileInfo(newLocation);
                fileInfo.IsReadOnly = false;
            }

            try
            {
                // First pass to allow cache to kick-in
                // NB : Using FILE_FLAG_NOBUFFERING causes exceptions due to seeking operations not being an integer multiple of the volume sector size
                // (see http://stackoverflow.com/questions/29234340/filestream-setlength-the-parameter-is-incorrect)
                Perf_Massread_noFileOptions(); 

                ticksBefore = System.DateTime.Now.Ticks;

                Perf_Massread_noFileOptions();

                ticksNow = System.DateTime.Now.Ticks;
                System.Console.WriteLine("No file options / buffer 4096 : " + (ticksNow-ticksBefore)/10000+ " ms");
                ticksBefore = ticksNow;

                Perf_Massread_randomAccess();

                ticksNow = System.DateTime.Now.Ticks;
                System.Console.WriteLine("Random Access / buffer 4096 : " + (ticksNow - ticksBefore) / 10000 + " ms");
                ticksBefore = ticksNow;

                Perf_Massread_RA_buf8192();

                ticksNow = System.DateTime.Now.Ticks;
                System.Console.WriteLine("Random Access / buffer 8192 : " + (ticksNow - ticksBefore) / 10000 + " ms");
                ticksBefore = ticksNow;

                Perf_Massread_RA_buf2048();

                ticksNow = System.DateTime.Now.Ticks;
                System.Console.WriteLine("Random Access / buffer 2048 : " + (ticksNow - ticksBefore) / 10000 + " ms");
                ticksBefore = ticksNow;

            } finally
            {
                // Mass delete resulting files
                for (int i = 0; i < NB_COPIES; i++)
                {
                    File.Delete(getNewLocation(i));
                }
            }
        }

        public void Perf_Massread_noFileOptions()
        {
            IOBase.ChangeFileOptions(FileOptions.None);
            IOBase.ChangeBufferSize(4096);

            performMassRead();
        }

        public void Perf_Massread_randomAccess()
        {
            IOBase.ChangeFileOptions(FileOptions.RandomAccess);
            IOBase.ChangeBufferSize(4096);

            performMassRead();
        }

        public void Perf_Massread_RA_buf8192()
        {
            IOBase.ChangeFileOptions(FileOptions.RandomAccess);
            IOBase.ChangeBufferSize(8192);

            performMassRead();
        }

        public void Perf_Massread_RA_buf2048()
        {
            IOBase.ChangeFileOptions(FileOptions.RandomAccess);
            IOBase.ChangeBufferSize(2048);

            performMassRead();
        }

        private void performMassRead()
        {
            // Mass-read resulting files
            for (int i = 0; i < NB_COPIES; i++)
            {
                //Track theTrack = new Track(getNewLocation(i)); // Old call still leads to old code
                AudioDataIOFactory.GetInstance().GetDataReader(getNewLocation(i)).ReadFromFile();
            }
        }
    }
}
