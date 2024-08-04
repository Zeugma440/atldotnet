using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using System.Collections;
using ATL.AudioData;

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

            /*
            PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(@"D:\temp\flac\272\cover.jpg"),
                PictureInfo.PIC_TYPE.Front,
                MetaDataIOFactory.TagType.RECOMMENDED
            );
            */
            PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"));
            theFile.EmbeddedPictures.Add(pic);

            theFile.Save();

            theFile = new Track(filePath);

            theFile.EmbeddedPictures.Add(pic);

            theFile.Save();

            System.Console.WriteLine("=====WRITE2 OK");

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
