using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;

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
        public void Perf_Write()
        {
            performWrite();
        }

        private void displayProgress(float progress)
        {
            Console.WriteLine(progress * 100 + "%");
        }

        private void performWrite()
        {
            // Mass-read resulting files
            foreach (string s in tempFiles) performWrite(s);
        }

        public void performWrite(String filePath)
        {
            new ConsoleLogger();
            Track theFile = new Track(filePath);

            double tDuration = theFile.DurationMs;
            long lDataOffset = theFile.TechnicalInformation.AudioDataOffset;
            long lDataAudioSize = theFile.TechnicalInformation.AudioDataSize;
            theFile.Chapters[0].Title += 'x';
            theFile.Chapters[0].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg"));
            //theFile.Chapters[0].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes("M:\\Temp\\Audio\\avatarOfSorts.png"));
            theFile.Chapters[0].Picture.ComputePicHash();
            theFile.Chapters[1].Title += 'x';
            theFile.Chapters[1].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic2.jpeg"));
            //theFile.Chapters[1].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes("M:\\Temp\\Audio\\themeOfTheTrack.jpg"));
            theFile.Chapters[1].Picture.ComputePicHash();

            theFile.Save();
            theFile = new Track(filePath);
            /*
            theFile.Chapters[0].Picture.ComputePicHash();
            Console.WriteLine(theFile.Chapters[0].Picture.PictureHash);
            theFile.Chapters[1].Picture.ComputePicHash();
            Console.WriteLine(theFile.Chapters[1].Picture.PictureHash);

            theFile.Chapters[1].Picture = null;

            theFile.Save();
            *
            */
            System.Console.WriteLine(theFile.Album);
                }

    }
}
