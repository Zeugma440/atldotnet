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

            //            Settings.tag

            // Modify metadata
//            theTrack.Title = "yeepee";
//            theTrack.Comment = "hohoho";
            theTrack.Artist = "Hey ho";
            theTrack.Composer = "Oscar Wilde";
            theTrack.Album = "Fake album starts here and is longer than the original one";

            theTrack.AdditionalFields.Add("bob", "theBuilder");
            /*
                        theTrack.AdditionalFields.Add("©mvi", "8");
                        theTrack.AdditionalFields.Add("©mvc", "9");
                        theTrack.AdditionalFields.Add("shwm", "10");
                        */

            /*
            if (theTrack.EmbeddedPictures.Count > 0) theTrack.EmbeddedPictures.Clear();

            byte[] data = File.ReadAllBytes(@"E:\temp\mp3\windowsIcon\folder.jpg");
            PictureInfo newPicture = PictureInfo.fromBinaryData(data, PictureInfo.PIC_TYPE.Front);
            theTrack.EmbeddedPictures.Add(newPicture);
            */

            //Settings.ID3v2_tagSubVersion = 3;
            //Settings.ID3v2_forceUnsynchronization = true;


            //theTrack.Chapters = new System.Collections.Generic.List<ChapterInfo>();

            /*
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
            */
            /*
            theTrack.Chapters.Add(new ChapterInfo(0, "Prologue: Mei"));
            theTrack.Chapters.Add(new ChapterInfo(790 * 1000, "Chapter One: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(2214 * 1000, "Chapter Three: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(3241 * 1000, "Chapter Four: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(4631 * 1000, "Chapter Five: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(5961 * 1000, "Chapter Six: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(7409 * 1000, "Chapter Seven: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(8754 * 1000, "Chapter Eight: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(10142 * 1000, "Chapter Nine: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(11388 * 1000, "Chapter Ten: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(12939 * 1000, "Chapter Eleven: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(14321 * 1000, "Chapter Twelve: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(15656 * 1000, "Chapter Thirteen: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(17096 * 1000, "Chapter Fourteen: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(18310 * 1000, "Chapter Fifteen: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(19737 * 1000, "Chapter Sixteen: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(21303 * 1000, "Chapter Seventeen: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(22635 * 1000, "Chapter Eighteen: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(24094 * 1000, "Chapter Nineteen: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(25587 * 1000, "Chapter Twenty: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(26948 * 1000, "Chapter Twenty-One: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(28346 * 1000, "Chapter Twenty-Two: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(29910 * 1000, "Chapter Twenty-Three: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(31241 * 1000, "Chapter Twenty-Four: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(32770 * 1000, "Chapter Twenty-Five: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(34141 * 1000, "Chapter Twenty-Six: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(35862 * 1000, "Chapter Twenty-Seven: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(37293 * 1000, "Chapter Twenty-Eight: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(38836 * 1000, "Chapter Twenty-Nine: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(40315 * 1000, "Chapter Thirty: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(41690 * 1000, "Chapter Thirty-One: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(43093 * 1000, "Chapter Thirty-Two: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(44558 * 1000, "Chapter Thirty-Three: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(45862 * 1000, "Chapter Thirty-Four: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(47178 * 1000, "Chapter Thirty-Five: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(48480 * 1000, "Chapter Thirty-Six: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(49912 * 1000, "Chapter Thirty-Seven: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(51240 * 1000, "Chapter Thirty-Eight: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(52582 * 1000, "Chapter Thirty-Nine: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(54119 * 1000, "Chapter Forty: Prax"));
            theTrack.Chapters.Add(new ChapterInfo(55391 * 1000, "Chapter Forty-One: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(56870 * 1000, "Chapter Forty-Two: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(58300 * 1000, "Chapter Forty-Three: Bobbie"));
            theTrack.Chapters.Add(new ChapterInfo(59543 * 1000, "Chapter Forty-Four: Holden"));
            theTrack.Chapters.Add(new ChapterInfo(60913 * 1000, "Chapter Forty-Five: Avasarala"));
            theTrack.Chapters.Add(new ChapterInfo(61909 * 1000, "Chapter Forty-Six: Bobbie"));
            */

            // Save modifications on disk
            theTrack.Save();
        }
    }
}
