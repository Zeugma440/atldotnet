using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Collections;

namespace ATL.benchmark
{
    [MemoryDiagnoser]
    public class AsyncSyncLegacy
    {
        //static string LOCATION = TestUtils.GetResourceLocationRoot()+"MP3/01 - Title Screen_pic.mp3";
        [Params(@"FLAC/flac.flac", @"MP4/mp4.m4a", @"MP3/id3v2.4_UTF8.mp3", @"WAV/wav.wav")]
        public string initialFileLocation;

        private IList<string> tempFiles = new List<string>();


        [IterationSetup]
        public void Setup()
        {
            tempFiles.Clear();
            // Duplicate resource
            tempFiles.Add(TestUtils.GenerateTempTestFile(initialFileLocation));
        }

        [IterationCleanup]
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
        public void Perf_WriteNoProgress()
        {
            performWrite(false);
        }

        [Benchmark]
        public void Perf_WriteProgress()
        {
            performWrite(true);
        }

        [Benchmark]
        public async Task Perf_WriteAsyncNoProgress()
        {
            await performWriteAsync(false);
        }

        [Benchmark]
        public async Task Perf_WriteAsyncProgress()
        {
            await performWriteAsync(true);
        }

        private void displayProgress(float progress)
        {
            //Console.WriteLine(progress * 100 + "%");
            // Nothing; don't pollute performance with display instruction
        }

        private void performWrite(bool withProgress)
        {
            // Mass-read resulting files
            foreach (string s in tempFiles) performWrite(s, withProgress);
        }

        private async Task performWriteAsync(bool withProgress)
        {
            // Mass-read resulting files
            foreach (string s in tempFiles) await performWriteAsync(s, withProgress);
        }

        public void performWrite(String filePath, bool withProgress)
        {
            Action<float> progress = new Action<float>(displayProgress);

            Track theFile = new Track(filePath);

            theFile.Title += "xoxo";
            theFile.EmbeddedPictures.Add(PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg")));
            /*
        theFile.Chapters[0].Title += 'x';
        theFile.Chapters[0].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg"));
        theFile.Chapters[0].Picture.ComputePicHash();
        theFile.Chapters[1].Title += 'x';
        theFile.Chapters[1].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic2.jpeg"));
        theFile.Chapters[1].Picture.ComputePicHash();
            */

            theFile.Save(withProgress ? progress : null);
        }

        public async Task performWriteAsync(String filePath, bool withProgress)
        {
            IProgress<float> progress = new Progress<float>(displayProgress);

            Track theFile = new Track(filePath);

            theFile.Title += "xoxo";
            theFile.EmbeddedPictures.Add(PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg")));
            /*
        theFile.Chapters[0].Title += 'x';
        theFile.Chapters[0].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg"));
        theFile.Chapters[0].Picture.ComputePicHash();
        theFile.Chapters[1].Title += 'x';
        theFile.Chapters[1].Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic2.jpeg"));
        theFile.Chapters[1].Picture.ComputePicHash();
            */

            await theFile.SaveAsync(withProgress ? progress : null);
        }
    }
}
