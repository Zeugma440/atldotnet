using ATL.AudioData;
using System.IO;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;

namespace ATL.benchmark
{
    public class BufferSizes
    {
        // NB : Using FILE_FLAG_NOBUFFERING causes exceptions due to seeking operations not being an integer multiple of the volume sector size
        // (see http://stackoverflow.com/questions/29234340/filestream-setlength-the-parameter-is-incorrect)
        const FileOptions FILE_FLAG_NOBUFFERING = (FileOptions)0x20000000;

        int nbCopies = 10;


        //static string LOCATION = TestUtils.GetResourceLocationRoot()+"MP3/01 - Title Screen_pic.mp3";
        [Params("MP3/01 - Title Screen_pic.mp3", "OGG/ogg_bigPicture.ogg")]
        public string initialFileLocation;

        private IList<string> tempFiles = new List<string>();


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

        [GlobalSetup]
        public void Setup()
        {
            tempFiles.Clear();
            // Duplicate resource
            for (int i = 0; i < nbCopies; i++)
            {
                tempFiles.Add( TestUtils.GenerateTempTestFile(initialFileLocation, i) );
            }

            // First pass to allow cache to kick-in
            Perf_Massread_NO_buf4096();
        }

        [GlobalCleanup]
        public void Cleanup()
        {
            // Mass delete resulting files
            foreach(string s in tempFiles)
            {
                File.Delete(s);
            }

            tempFiles.Clear();
        }

        [Benchmark(Baseline =true)]
        public void Perf_Massread_NO_buf4096()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.None);
            AudioDataManager.ChangeBufferSize(4096);

            performMassRead();
        }

        [Benchmark]
        public void Perf_Massread_RA_buf4096()
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
        public void Perf_Massread_RA_buf512()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.RandomAccess);
            AudioDataManager.ChangeBufferSize(512);

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

        [Benchmark]
        public void Perf_Massread_NO_buf512()
        {
            AudioDataManager.ChangeFileOptions(FileOptions.None);
            AudioDataManager.ChangeBufferSize(512);

            performMassRead();
        }

        private void performMassRead()
        {
            // Mass-read resulting files
            foreach(string s in tempFiles)
            {
                //Track theTrack = new Track(getNewLocation(i)); // Old call still leads to old code

                //new AudioDataManager( AudioDataIOFactory.GetInstance().GetDataReader(s) ).ReadFromFile(); // Does not load any picture

                Track t = new Track(s);
                t.GetEmbeddedPicture();
            }
        }
    }
}
