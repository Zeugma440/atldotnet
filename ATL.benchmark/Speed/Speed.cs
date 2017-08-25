using ATL.AudioData;
using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;

namespace ATL.benchmark
{
    public class Speed
    {
        const int NB_COPIES = 20;
        const FileOptions FILE_FLAG_NOBUFFERING = (FileOptions)0x20000000;

        static string LOCATION = TestUtils.GetResourceLocationRoot()+"MP3/01 - Title Screen_pic.mp3";

        private static string getNewLocation(int index)
        {
            return LOCATION.Replace("MP3/", "tmp/" + index.ToString());
        }

        public void Perf_Method()
        {
            ulong test = 32974337984693648;
            long test2 = 32974337984693648;

            long max = 100000000;
            long ticksBefore, ticksNow;

            ticksBefore = System.DateTime.Now.Ticks;

            for (long i = 0; i< max; i++)
            {
                StreamUtils.ReverseUInt64(test);
            }
            ticksNow = System.DateTime.Now.Ticks;

            System.Console.WriteLine("ReverseUInt64 : " + (ticksNow - ticksBefore) / 10000 + " ms");


            ticksBefore = System.DateTime.Now.Ticks;

            for (long i = 0; i < max; i++)
            {
                StreamUtils.ReverseInt64(test2);
            }
            ticksNow = System.DateTime.Now.Ticks;

            System.Console.WriteLine("ReverseInt64 : " + (ticksNow - ticksBefore) / 10000 + " ms");
        }

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

        [GlobalSetup]
        public void Setup()
        {
            // Duplicate resource
            for (int i = 0; i < NB_COPIES; i++)
            {
                string newLocation = getNewLocation(i);
                File.Copy(LOCATION, newLocation, true);

                FileInfo fileInfo = new FileInfo(newLocation);
                fileInfo.IsReadOnly = false;
            }

            // First pass to allow cache to kick-in
            Perf_Massread_noFileOptions();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Mass delete resulting files
            for (int i = 0; i < NB_COPIES; i++)
            {
                File.Delete(getNewLocation(i));
            }
        }

        [Benchmark]
        public void Perf_Massread_noFileOptions()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.None);
            AudioDataManager.ChangeBufferSize(4096);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_randomAccess()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.RandomAccess);
            AudioDataManager.ChangeBufferSize(4096);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_RA_buf8192()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.RandomAccess);
            AudioDataManager.ChangeBufferSize(8192);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_RA_buf2048()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.RandomAccess);
            AudioDataManager.ChangeBufferSize(2048);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_RA_buf1024()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.RandomAccess);
            AudioDataManager.ChangeBufferSize(1024);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_NO_buf2048()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.None);
            AudioDataManager.ChangeBufferSize(2048);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_NO_buf1024()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.None);
            AudioDataManager.ChangeBufferSize(1024);

            performMassRead();
        }

        private void performMassRead()
        {
            // Mass-read resulting files
            for (int i = 0; i < NB_COPIES; i++)
            {
                //Track theTrack = new Track(getNewLocation(i)); // Old call still leads to old code
                new AudioDataManager( AudioDataIOFactory.GetInstance().GetDataReader(getNewLocation(i))).ReadFromFile();
            }
        }
    }
}
