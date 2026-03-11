using ATL.AudioData;
using ATL.CatalogDataReaders;
using ATL.Logging;
using ATL.Playlist;
using Commons;

namespace ATL.test.CodeSnippets

{
#pragma warning disable S2699 // Tests should include assertions

    [TestClass]
    public class CodeSnippets : ILogDevice
    {
        string audioFilePath;
        string cuesheetFilePath;
        string playlistFilePath;
        readonly string playlistPath = TestUtils.GetResourceLocationRoot() + "_Playlists/playlist_simple.m3u";
        readonly string imagePath1 = TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpeg";
        readonly string imagePath2 = TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpeg";

        readonly Log theLog = new Log();
        readonly System.Collections.Generic.IList<Log.LogItem> messages = new System.Collections.Generic.List<Log.LogItem>();


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

            // Display standard fields
            System.Console.WriteLine("Title : " + theTrack.Title);
            System.Console.WriteLine("Artist : " + theTrack.Artist);
            System.Console.WriteLine("Album : " + theTrack.Album);
            System.Console.WriteLine("Recording year : " + theTrack.Year);
            System.Console.WriteLine("Track number : " + theTrack.TrackNumber);
            System.Console.WriteLine("Disc number : " + theTrack.DiscNumber);
            System.Console.WriteLine("Genre : " + theTrack.Genre);
            System.Console.WriteLine("Comment : " + theTrack.Comment);

            // Display standard fields (multiple values)
            var artists = theTrack.Artist.Split(ATL.Settings.DisplayValueSeparator);
            foreach (var artist in artists) System.Console.WriteLine("Artist : " + artist);

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
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "<Pending>")]
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
                // do stuff
                image.Dispose();
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_UpdatingMetadataText()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);


            // Modify metadata

            // Set standard values
            theTrack.Artist = "Hey ho";
            theTrack.Composer = "Oscar Wilde";
            // Set multiple standard values
            theTrack.Genre = "Country" + ATL.Settings.DisplayValueSeparator + "Disco";
            // Set non-standard values
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
            PictureInfo newPicture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(imagePath1), PictureInfo.PIC_TYPE.CD);
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
            ch.StartTime = 0;
            ch.UniqueID = ""; // Unique ID is specific to ID3v2 chapters
            ch.Title = "title1";
            ch.Subtitle = "subtitle1"; // Substitle is specific to ID3v2 chapters
            ch.Url = new ChapterInfo.UrlInfo("somewhere", "https://some.whe.re");  // Chapter URL is specific to ID3v2 chapters
            ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(imagePath1)); // Pictures are supported by ID3v2 and MP4/M4A
            theFile.Chapters.Add(ch);

            ch = new ChapterInfo(1230, "title2"); // Faster that way :-)
            ch.UniqueID = "002";
            ch.Subtitle = "subtitle2";
            ch.Url = new ChapterInfo.UrlInfo("anywhere", "https://any.whe.re");
            ch.Picture = PictureInfo.fromBinaryData(System.IO.File.ReadAllBytes(imagePath2));
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

            theFile.Lyrics = new List<LyricsInfo>();
            LyricsInfo info = new LyricsInfo();
            theFile.Lyrics.Add(info);

            info.LanguageCode = "eng";
            info.Description = "song";

            // Option A : Unsynchronized lyrics
            info.UnsynchronizedLyrics = "I'm the one\r\n中を";

            // Option B : Synchronized lyrics
            info.ContentType = LyricsInfo.LyricsType.LYRICS;
            info.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase(12000, "I'm the one")); // 12s timestamp
            info.SynchronizedLyrics.Add(new LyricsInfo.LyricsPhrase("00:00:45", "中を"));   // 45s timestamp

            // Option C : Synchronized lyrics (from LRC or SRT formats)
            info.UnsynchronizedLyrics = "[00:28.581]<00:28.581>I'm <00:28.981>wishing <00:29.797>on <00:30.190>a <00:30.629>star<00:31.575>\r\n[00:31.877]<00:31.877>And <00:32.245>trying <00:33.109>to <00:33.525>believe<00:34.845>\r\n";

            // Persists the chapters
            theFile.Save();

            // Reads the file again
            theFile = new Track(audioFilePath);

            // Display lyrics
            foreach (LyricsInfo lyrics in theFile.Lyrics)
            {
                System.Console.WriteLine(lyrics.UnsynchronizedLyrics);
                foreach (LyricsInfo.LyricsPhrase phrase in lyrics.SynchronizedLyrics)
                {
                    System.Console.WriteLine("[" + Utils.EncodeTimecode_ms(phrase.TimestampStart) + "] " + phrase.Text);
                }
            }
        }

        [TestMethod, TestCategory("snippets")]
        public void CS_BroadcastWave()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);


            // Display BEXT, LIST INFO and iXML data
            string originator = "", engineer = "", scene = "";
            if (theTrack.AdditionalFields.TryGetValue("bext.originator", out var field)) originator = field;
            if (theTrack.AdditionalFields.TryGetValue("info.IENG", out var field2)) engineer = field2;
            if (theTrack.AdditionalFields.TryGetValue("ixml.SCENE", out var field3)) scene = field3;

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
        public void CS_XMP()
        {
            // Load audio file information into memory
            Track theTrack = new Track(audioFilePath);


            // Display XMP data
            string photoshopSource = "", rating = "", composer = "";
            if (theTrack.AdditionalFields.TryGetValue("xmp.rdf:RDF.rdf:Description.photoshop:Source", out var field)) photoshopSource = field;
            if (theTrack.AdditionalFields.TryGetValue("xmp.rdf:RDF.rdf:Description.xmp:Rating", out var field2)) rating = field2;
            if (theTrack.AdditionalFields.TryGetValue("xmp.rdf:RDF.rdf:Description.xmpDM:composer", out var field3)) composer = field3;

            System.Console.WriteLine("Source : " + photoshopSource);
            System.Console.WriteLine("Rating : " + rating);
            System.Console.WriteLine("Composer : " + composer);


            // Modify data
            theTrack.AdditionalFields["xmp.rdf:RDF.rdf:Description.photoshop:Source"] = "Company A";
            theTrack.AdditionalFields["xmp.rdf:RDF.rdf:Description.xmp:Rating"] = "5";
            theTrack.AdditionalFields["xmp.rdf:RDF.rdf:Description.xmpDM:composer"] = "Dave Johnson";
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
            pls.Save();

            // Writing tracks
            IList<Track> tracksToWrite = new List<Track>();
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MP3/empty.mp3"));
            tracksToWrite.Add(new Track(TestUtils.GetResourceLocationRoot() + "MOD/mod.mod"));
            pls.Tracks = tracksToWrite;
            pls.Save();
        }
    }
#pragma warning restore S2699 // Tests should include assertions

}
