using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ATL.UI_test
{
    public partial class Form1 : Form
    {
        private const string filePath = @"D:\temp\m4a-mp4\98\03 The Front.m4a";

        public Form1()
        {
            InitializeComponent();
        }

        private void displayProgress(float progress)
        {
            displayProgress(progress, false);
        }

        private void displayProgressForceRefresh(float progress)
        {
            displayProgress(progress, true);
        }

        private void displayProgress(float progress, bool needForceRefresh)
        {
            String str = (progress * 100).ToString() + "%";
            ProgressLbl.Text = str;
            if (needForceRefresh) Application.DoEvents();
        }

        private void GoSyncBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            processFile(filePath, "sync", false).Wait();
        }

        private void GoSyncProgressBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            Action<float> progress = new Action<float>(displayProgressForceRefresh);
            processFile(filePath, "legacy", false, progress).Wait();
        }

        private async void GoAsyncProgressBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            Action<float> progress = new Action<float>(displayProgress);
            await processFile(filePath, "async progress", true, progress);
        }

        private async void GoAsyncBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            await processFile(filePath, "async silent", true);
        }

        private async Task<bool> processFile(string path, string method, bool asynchronous, Action<float> progress = null)
        {
            ProgressLbl.Text = "Preparing temp file...";
            Application.DoEvents();
            string testFileLocation = "";
            if (asynchronous)
            {
                testFileLocation = await TestUtils.GenerateTempTestFileAsync(path);
            }
            else
            {
                testFileLocation = TestUtils.GenerateTempTestFile(path);
            }

            long timestamp = DateTime.Now.Ticks;
            Settings.FileBufferSize = 4 * 1024 * 1024;
            try
            {
                ProgressLbl.Text = "Reading...";
                Application.DoEvents();

                Track theFile = new Track(testFileLocation);

                /*
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
                */
                theFile.Title += "aaa";
                theFile.EmbeddedPictures.Add(PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(@"C:\Users\zeugm\source\repos\Zeugma440\atldotnet\ATL.test\Resources\_Images\pic1.jpeg")));

                ProgressLbl.Text = "Saving...";
                Application.DoEvents();

                if (asynchronous)
                    return await theFile.SaveAsync((null == progress) ? null : new Progress<float>(progress));
                else
                    return theFile.Save(progress);
                /*
                theFile = new Track(testFileLocation);

                theFile.Chapters[0].Picture.ComputePicHash();
                Console.WriteLine(theFile.Chapters[0].Picture.PictureHash);
                theFile.Chapters[1].Picture.ComputePicHash();
                Console.WriteLine(theFile.Chapters[1].Picture.PictureHash);

                theFile.Chapters[1].Picture = null;

                await theFile.SaveAsync();
                */
            }
            finally
            {
                long timestamp2 = DateTime.Now.Ticks;
                Console.WriteLine("Total time : " + (timestamp2 - timestamp) / 1000.0 + "[" + method + "]");

                ProgressLbl.Text = "Done !";
                Application.DoEvents();
                File.Delete(testFileLocation);
            }
        }
    }
}
