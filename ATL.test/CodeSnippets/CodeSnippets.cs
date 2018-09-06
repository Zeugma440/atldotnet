using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using ATL.PlaylistReaders;
using ATL.CatalogDataReaders;
using ATL.Logging;
using System.IO;

namespace ATL.test.CodeSnippets
{
    [TestClass]
    public class CodeSnippets : ILogDevice
    {
        string audioFilePath;
        string cuesheetPath;
        string playlistPath = TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u";
        string imagePath = TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg";

        Log theLog = new Log();
        System.Collections.Generic.IList<Log.LogItem> messages = new System.Collections.Generic.List<Log.LogItem>();


        public CodeSnippets()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        [TestInitialize]
        public void Init()
        {
            audioFilePath = TestUtils.GetTempTestFile("MP3/id3v2.3_UTF16.mp3");
            cuesheetPath = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Cuesheet/cue.cue", "$PATH", TestUtils.GetResourceLocationRoot(false));
        }

        [TestCleanup]
        public void End()
        {
            File.Delete(audioFilePath);
            File.Delete(cuesheetPath);
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingAudioFileText()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);

            // Display classic 'supported' fields
            System.Console.WriteLine("Title : " + theTrack.Title);
            System.Console.WriteLine("Artist : " + theTrack.Artist);
            System.Console.WriteLine("Album : " + theTrack.Album);
            System.Console.WriteLine("Recording year : " + theTrack.Year);
            System.Console.WriteLine("Track number : " + theTrack.TrackNumber);
            System.Console.WriteLine("Disc number : " + theTrack.DiscNumber);
            System.Console.WriteLine("Genre : " + theTrack.Genre);
            System.Console.WriteLine("Comment : " + theTrack.Comment);

            System.Console.WriteLine("Duration (s) : " + theTrack.Duration);
            System.Console.WriteLine("Bitrate (KBps) : " + theTrack.Bitrate);

            System.Console.WriteLine("Has this file variable bitrate audio : " + (theTrack.IsVBR ? "yes" : "no"));
            System.Console.WriteLine("Has this file lossless audio : " + (AudioDataIOFactory.CF_LOSSLESS == theTrack.CodecFamily ? "yes" : "no"));

            // Display any additional 'unsupported' fields (e.g. TXXX values in ID3v2, or any other custom tag)
            foreach (System.Collections.Generic.KeyValuePair<string, string> field in theTrack.AdditionalFields)
            {
                System.Console.WriteLine("Field " + field.Key + " : value = " + field.Value);
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingAudioFilePictures()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);

            // Get picture list
            System.Collections.Generic.IList<PictureInfo> embeddedPictures = theTrack.EmbeddedPictures;

            // Transform them into .NET Image, if needed
            foreach (PictureInfo pic in embeddedPictures)
            {
                System.Drawing.Image image = System.Drawing.Image.FromStream((new System.IO.MemoryStream(pic.PictureData)));
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_UpdatingMetadataText()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);

            // Modify metadata
            theTrack.Artist = "Hey ho";
            theTrack.Composer = "Oscar Wilde";

            // Save modifications on the disc
            theTrack.Save();
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_UpdatingMetadataPictures()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);

            // Delete first embedded picture (let's say it exists)
            theTrack.EmbeddedPictures.RemoveAt(0);

            // Add 'CD' embedded picture
            PictureInfo newPicture = new PictureInfo(Commons.ImageFormat.Gif, PictureInfo.PIC_TYPE.CD);
            newPicture.PictureData = File.ReadAllBytes(imagePath);
            theTrack.EmbeddedPictures.Add(newPicture);

            // Save modifications on the disc
            theTrack.Save();
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_WriteChapters()
        {
            AudioDataManager theFile = new AudioDataManager(AudioDataIOFactory.GetInstance().GetFromPath(audioFilePath));

            TagData theTag = new TagData();
            theTag.Chapters = new System.Collections.Generic.List<ChapterInfo>();

	        ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 123;
	        ch.StartOffset = 456;
	        ch.EndTime = 789;
	        ch.EndOffset = 101112;
	        ch.UniqueID = "";
	        ch.Title = "aaa";
	        ch.Subtitle = "bbb";
	        ch.Url = "ccc\0ddd";
	        theTag.Chapters.Add(ch);

	        ch = new ChapterInfo();
            ch.StartTime = 1230;
	        ch.StartOffset = 4560;
	        ch.EndTime = 7890;
	        ch.EndOffset = 1011120;
	        ch.UniqueID = "002";
	        ch.Title = "aaa0";
	        ch.Subtitle = "bbb0";
	        ch.Url = "ccc\0ddd0";
	        theTag.Chapters.Add(ch);

	        // Persists the chapters
	        theFile.UpdateTagInFile(theTag, MetaDataIOFactory.TAG_ID3V2);

	        // Reads them
	        theFile.ReadFromFile(false, true);

	        foreach (ChapterInfo chap in theFile.ID3v2.Chapters)
	        {
		        System.Console.WriteLine(chap.Title + "(" + chap.StartTime + ")");
	        }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_BroadcastWave()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);


            // Display BEXT, LIST INFO and iXML data
            string originator = "", engineer = "", scene = "";
            if (theTrack.AdditionalFields.ContainsKey("bext.originator")) originator = theTrack.AdditionalFields["bext.originator"];
            if (theTrack.AdditionalFields.ContainsKey("info.IENG")) engineer = theTrack.AdditionalFields["info.IENG"];
            if (theTrack.AdditionalFields.ContainsKey("ixml.SCENE")) scene = theTrack.AdditionalFields["ixml.SCENE"];

            System.Console.WriteLine("Originator : " + originator);
            System.Console.WriteLine("Engineer : " + engineer);
            System.Console.WriteLine("Scene : " + scene);


            // Modify data
            theTrack.AdditionalFields["bext.originator"] = "Dave Johnson";
            theTrack.AdditionalFields["info.IENG"] = "John Jackman";
            theTrack.AdditionalFields["ixml.SCENE"] = "42";
            theTrack.Save();
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingPlaylist()
        {
            IPlaylistReader theReader = PlaylistReaderFactory.GetInstance().GetPlaylistReader(playlistPath);

            foreach (string s in theReader.GetFiles())
            {
                System.Console.WriteLine(s);
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingCuesheet()
        {
            ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(cuesheetPath);

            System.Console.WriteLine(theReader.Artist);
            System.Console.WriteLine(theReader.Title);
            System.Console.WriteLine(theReader.Comments);
            foreach (Track t in theReader.Tracks)
            {
                System.Console.WriteLine(">" + t.Title);
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ListingSupportedFormats()
        {
            System.Text.StringBuilder filter = new System.Text.StringBuilder("");

            foreach (Format f in PlaylistReaderFactory.GetInstance().getFormats())
            {
                if (f.Readable)
                {
                    foreach (string extension in f)
                    {
                        filter.Append(extension).Append(";");
                    }
                }
            }
            // Removes the last separator
            filter.Remove(filter.Length - 1, 1);
        }

        [TestMethod, TestCategory("snippets")]
        public void TestSyncMessage()
        {
            messages.Clear();

            LogDelegator.GetLocateDelegate()("file name");
            LogDelegator.GetLogDelegate()(Log.LV_DEBUG, "test message 1");
            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "test message 2");

            System.Console.WriteLine(messages[0].Message);
        }

        public void DoLog(Log.LogItem anItem)
        {
            messages.Add(anItem);
        }
    }
}
