using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;

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
            IProgress<float> progress = new Progress<float>(displayProgress);
            Track theTrack = new Track(filePath, progress);

            // Modify metadata
            theTrack.Artist = "Hey ho";
            theTrack.Composer = "Oscar Wilde";
            theTrack.Album = "Fake album starts here and is longer than the original one";

            /*
            if (theTrack.EmbeddedPictures.Count > 0) theTrack.EmbeddedPictures.Clear();

            byte[] data = File.ReadAllBytes(@"E:\temp\mp3\windowsIcon\folder.jpg");
            PictureInfo newPicture = PictureInfo.fromBinaryData(data, PictureInfo.PIC_TYPE.Front);
            theTrack.EmbeddedPictures.Add(newPicture);
            */

            //Settings.ID3v2_tagSubVersion = 3;
            //Settings.ID3v2_forceUnsynchronization = true;
            theTrack.Chapters = new System.Collections.Generic.List<ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 0;
            ch.StartOffset = 0;
            ch.EndTime = 12519;
            ch.EndOffset = 12519;
            ch.UniqueID = "";
            ch.Title = "malarky1";
            ch.Subtitle = "bobs your uncle now";
            theTrack.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 12519;
            ch.StartOffset = 12519;
            ch.EndTime = 102519;
            ch.EndOffset = 102519;
            ch.UniqueID = "";
            ch.Title = "malarky2";
            ch.Subtitle = "bobs your uncle now";
            theTrack.Chapters.Add(ch);


            ch = new ChapterInfo();
            ch.StartTime = 102519;
            ch.StartOffset = 102519;
            ch.EndTime = 3002519;
            ch.EndOffset = 3002519;
            ch.UniqueID = "";
            ch.Title = "malarky3";
            ch.Subtitle = "bobs your uncle now";
            theTrack.Chapters.Add(ch);

            // Save modifications on the disc

            theTrack.Save();
        }
    }
}
