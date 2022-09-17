using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ATL.UI_test
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void displayProgress(float progress)
        {
            String str = (progress * 100).ToString() + "%";
            //            Console.WriteLine(str);
            ProgressLbl.Text = str;
        }

        private void GoSyncBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            IProgress<float> progress = new Progress<float>(displayProgress);
            processFile(@"D:\temp\m4a-mp4\audiobooks\dragon_maiden_orig.m4b", "sync", false, false).Wait();
        }

        private void GoLegacyBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            processFile(@"D:\temp\m4a-mp4\audiobooks\dragon_maiden_orig.m4b", "legacy", false, true).Wait();
        }

        private async void GoAsyncBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            IProgress<float> progress = new Progress<float>(displayProgress);
            await processFile(@"D:\temp\m4a-mp4\audiobooks\dragon_maiden_orig.m4b", "async progress", true, false, progress);
        }

        private async void GoAsyncSilentBtn_Click(object sender, EventArgs e)
        {
            ProgressLbl.Text = "";
            ProgressLbl.Visible = true;

            await processFile(@"D:\temp\m4a-mp4\audiobooks\dragon_maiden_orig.m4b", "async silent", true, false);
        }

        private async Task<bool> processFile(string path, string method, bool asynchronous, bool legacy, IProgress<float> progress = null)
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

                Track theFile = new Track(testFileLocation, progress);

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

                ProgressLbl.Text = "Saving...";
                Application.DoEvents();

                if (asynchronous)
                    return await theFile.SaveAsync();
                else
                    return theFile.Save();
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
