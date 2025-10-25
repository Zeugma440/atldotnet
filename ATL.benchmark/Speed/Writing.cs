using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Running;
using System.Collections;
using ATL.AudioData;
using Commons;

namespace ATL.benchmark
{
    public class Writing
    {
        //static string LOCATION = TestUtils.GetResourceLocationRoot()+"MP3/01 - Title Screen_pic.mp3";
        [Params(@"FLAC/flac.flac")] public string initialFileLocation;

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
            Track theFile;


            theFile = new Track(filePath);


            //PictureInfo pic = PictureInfo.fromBinaryData(File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg"));
            //var bigData = new byte[3500];
            //theFile.AdditionalFields["booh"] = Utils.Latin1Encoding.GetString(bigData);

            //theFile.Remove();
            //            theFile.Save();

            //theFile = new Track(filePath);
            //theFile.EmbeddedPictures.Add(pic);
            theFile.Title = "flash! a-hah!!";

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
            System.Console.WriteLine(theFile.Title);

            /*
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.ReadWrite, FileShare.Read))
            {
                theFile = new Track(fs);
                //theFile = new Track(filePath);
            }
        */
        }

        public void performRemove(String filePath, int times)
        {
            Track theFile = new Track(filePath);
            System.Console.WriteLine("=====REMOVE START");
            for (int i = 0; i < times; i++)
            {
                theFile.Remove(MetaDataIOFactory.TagType.NATIVE);

                theFile.Save();
                System.Console.WriteLine("=====REMOVE" + i + " OK");
                theFile = new Track(filePath);
            }

            System.Console.WriteLine(theFile.Title);
        }
    }
}
