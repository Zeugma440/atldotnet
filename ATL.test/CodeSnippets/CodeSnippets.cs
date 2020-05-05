using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using ATL.CatalogDataReaders;
using ATL.Logging;
using System.Collections.Generic;
using ATL.Playlist;
using Commons;

namespace ATL.test.CodeSnippets
{
    [TestClass]
    public class CodeSnippets : ILogDevice
    {
        string audioFilePath;
        string cuesheetFilePath;
        string playlistFilePath;
        string playlistPath = TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u";
        string imagePath = TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg";

        Log theLog = new Log();
        System.Collections.Generic.IList<Log.LogItem> messages = new System.Collections.Generic.List<Log.LogItem>();


        public CodeSnippets()
        {
            LogDelegator.SetLog(ref theLog);
            theLog.Register(this);
        }

        public void DoLog(Log.LogItem anItem)
        {
            messages.Add(anItem);
        }

        [TestInitialize]
        public void Init()
        {
            audioFilePath = TestUtils.CopyAsTempTestFile("MP3/id3v2.3_UTF16.mp3");
            playlistFilePath = TestUtils.CreateTempTestFile("playlist.test");
            cuesheetFilePath = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Cuesheet/cue.cue", "$PATH", TestUtils.GetResourceLocationRoot(false));
        }

        [TestCleanup]
        public void End()
        {
            System.IO.File.Delete(audioFilePath);
            System.IO.File.Delete(cuesheetFilePath);
            System.IO.File.Delete(playlistFilePath);
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
            System.Console.WriteLine("Number of channels : " + theTrack.ChannelsArrangement.NbChannels);
            System.Console.WriteLine("Channels arrangement : " + theTrack.ChannelsArrangement.Description);

            System.Console.WriteLine("Has this file variable bitrate audio : " + (theTrack.IsVBR ? "yes" : "no"));
            System.Console.WriteLine("Has this file lossless audio : " + (AudioDataIOFactory.CF_LOSSLESS == theTrack.CodecFamily ? "yes" : "no"));

            // Display custom fields (e.g. TXXX values in ID3v2, or any other custom tag)
            foreach (System.Collections.Generic.KeyValuePair<string, string> field in theTrack.AdditionalFields)
            {
                System.Console.WriteLine("Custom field " + field.Key + " : value = " + field.Value);
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
                System.Drawing.Image image = System.Drawing.Image.FromStream(new System.IO.MemoryStream(pic.PictureData));
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
            theTrack.AdditionalFields["customField"] = "fancyValue";


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
            PictureInfo newPicture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(imagePath), PictureInfo.PIC_TYPE.CD);
            theTrack.EmbeddedPictures.Add(newPicture);

            // Save modifications on the disc
            theTrack.Save();
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_WriteChapters()
        {
            // Note : if you target ID3v2 chapters, it is highly advised to use Settings.ID3v2_tagSubVersion = 3
            // as most readers only support ID3v2.3 chapters
            Track theFile = new Track(audioFilePath);

            theFile.Chapters = new System.Collections.Generic.List<ChapterInfo>();

            ChapterInfo ch = new ChapterInfo();
            ch.StartTime = 123;
            ch.StartOffset = 456;
            ch.EndTime = 789;
            ch.EndOffset = 101112;
            ch.UniqueID = "";
            ch.Title = "aaa";
            ch.Subtitle = "bbb";
            ch.Url = new ChapterInfo.UrlInfo("ccc", "ddd");
            theFile.Chapters.Add(ch);

            ch = new ChapterInfo();
            ch.StartTime = 1230;
            ch.StartOffset = 4560;
            ch.EndTime = 7890;
            ch.EndOffset = 1011120;
            ch.UniqueID = "002";
            ch.Title = "aaa0";
            ch.Subtitle = "bbb0";
            ch.Url = new ChapterInfo.UrlInfo("ccc", "ddd0");
            // Add a picture to the 2nd chapter
            ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(imagePath));
            theFile.Chapters.Add(ch);

            // Persists the chapters
            theFile.Save();

            // Reads the file again from sratch
            theFile = new Track(audioFilePath);
            IList<PictureInfo> pics = theFile.EmbeddedPictures; // Hack to load chapter pictures

            // Display chapters
            foreach (ChapterInfo chap in theFile.Chapters)
            {
                System.Console.WriteLine(chap.Title + "(" + chap.StartTime + ")");
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_WriteLyrics()
        {
            Track theFile = new Track(audioFilePath);

            theFile.Lyrics = new LyricsInfo();
            theFile.Lyrics.LanguageCode = "eng";
            theFile.Lyrics.Description = "song";

            // Option A : Unsynchronized lyrics
            theFile.Lyrics.UnsynchronizedLyrics = "I'm the one\r\n中を";

            // Option B : Synchronized lyrics
            theFile.Lyrics.ContentType = LyricsInfo.LyricsType.LYRICS;
            theFile.Lyrics.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase(12000, "I'm the one")); // 12s timestamp
            theFile.Lyrics.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase("00:00:45", "中を"));   // 45s timestamp

            // Persists the chapters
            theFile.Save();

            // Reads the file again
            theFile = new Track(audioFilePath);

            // Display lyrics
            System.Console.WriteLine(theFile.Lyrics.UnsynchronizedLyrics);
            foreach (LyricsInfo.LyricsPhrase phrase in theFile.Lyrics.SynchronizedLyrics)
            {
                System.Console.WriteLine("[" + Utils.EncodeTimecode_ms(phrase.TimestampMs) + "] " + phrase.Text);
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
        public void CS_WaveSample()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);


            // Display general data
            int manufacturer = 0;
            if (theTrack.AdditionalFields.ContainsKey("sample.manufacturer")) manufacturer = int.Parse(theTrack.AdditionalFields["sample.manufacturer"]);
            int midiUnityNote = 0;
            if (theTrack.AdditionalFields.ContainsKey("sample.MIDIUnityNote")) midiUnityNote = int.Parse(theTrack.AdditionalFields["sample.MIDIUnityNote"]);

            System.Console.WriteLine("Manufacturer : " + manufacturer);
            System.Console.WriteLine("MIDI Unity note : " + midiUnityNote);

            // Display loop points data
            int nbLoopPoints = 0;
            if (theTrack.AdditionalFields.ContainsKey("sample.NumSampleLoops")) nbLoopPoints = int.Parse(theTrack.AdditionalFields["sample.NumSampleLoops"]);

            for (int i = 0; i < nbLoopPoints; i++)
            {
                int type = 0;
                if (theTrack.AdditionalFields.ContainsKey("sample.SampleLoop[" + i + "].Type")) type = int.Parse(theTrack.AdditionalFields["sample.SampleLoop[" + i + "].Type"]);
                int start = 0;
                if (theTrack.AdditionalFields.ContainsKey("sample.SampleLoop[" + i + "].Start")) start = int.Parse(theTrack.AdditionalFields["sample.SampleLoop[" + i + "].Start"]);
                int end = 0;
                if (theTrack.AdditionalFields.ContainsKey("sample.SampleLoop[" + i + "].End")) end = int.Parse(theTrack.AdditionalFields["sample.SampleLoop[" + i + "].End"]);

                System.Console.WriteLine("Sample[" + i + "] : Type " + type + " : " + start + "->" + end);
            }

            // Modify data
            theTrack.AdditionalFields["sample.MIDIUnityNote"] = "61";
            theTrack.AdditionalFields["sample.SampleLoop[0].Start"] = "1000";
            theTrack.AdditionalFields["sample.SampleLoop[0].End"] = "2000";
            
            // Add new sample loop
            theTrack.AdditionalFields["sample.SampleLoop[1].Start"] = "3000";
            theTrack.AdditionalFields["sample.SampleLoop[1].End"] = "4000";
            
            // Remove sample loop (all sub-fields after [x] should be removed)
            theTrack.AdditionalFields.Remove("sample.SampleLoop[2].Start");
            theTrack.AdditionalFields.Remove("sample.SampleLoop[2].End");

            theTrack.Save();
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingPlaylist()
        {
            IPlaylistIO theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);

            foreach (string s in theReader.FilePaths)
            {
                System.Console.WriteLine(s);
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_ReadingCuesheet()
        {
            ICatalogDataReader theReader = CatalogDataReaderFactory.GetInstance().GetCatalogDataReader(cuesheetFilePath);

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

            foreach (Format f in PlaylistIOFactory.GetInstance().getFormats())
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

        [TestMethod, TestCategory("snippets")]
        public void TestWritePls()
        {
            IPlaylistIO pls = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistFilePath);

            // Writing file paths
            IList<string> pathsToWrite = new List<string>();
            pathsToWrite.Add("aaa.mp3");
            pathsToWrite.Add("bbb.mp3");
            pls.FilePaths = pathsToWrite;

            // Writing tracks
            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3/empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD/mod.mod"));
            pls.Tracks = tracksToWrite;
        }
    }
}
