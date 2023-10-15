using ATL.Playlist;

namespace ATL.test.IO.Playlist
{
    [TestClass]
    public abstract class PlaylistIOTest
    {
        protected static readonly IList<string> pathsToWrite = new List<string>();
        protected static readonly IList<Track> tracksToWrite = new List<Track>();
        protected static readonly string NEW_TITLE = "This is a new title";
        
        protected static string? remoteFilePath1;
        protected static string? remoteFilePath2;
        protected static string? localFilePath1;
        protected static string? testSubfolder;
        protected static string? localFilePath2;

        [AssemblyInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            // == PLIO_R
            string testTrackLocation1 = TestUtils.CopyAsTempTestFile("MP3/empty.mp3");
            string testTrackLocation2 = TestUtils.CopyAsTempTestFile("MOD/mod.mod");
            pathsToWrite.Add(testTrackLocation1);
            pathsToWrite.Add(testTrackLocation2);
            pathsToWrite.Add("http://this-is-a-stre.am:8405/live");

            foreach (var s in pathsToWrite) tracksToWrite.Add(new Track(s));

            // == PLIO_RW_Absolute_Relative_Path
            // Select a remote file to link
            remoteFilePath1 = TestUtils.GetResourceLocationRoot() + "MID" + Path.DirectorySeparatorChar + "yoru-uta.mid";
            remoteFilePath2 = TestUtils.GetResourceLocationRoot() + "MID" + Path.DirectorySeparatorChar + "memory.mid";
            // Move one files on temp folder (-> local file 1)
            localFilePath1 = TestUtils.CopyAsTempTestFile("MID/chron.mid");
            // Move one files on temp folder subfolder (-> local file 2)
            testSubfolder =
                Directory.CreateDirectory(TestUtils.GetResourceLocationRoot() + Path.DirectorySeparatorChar + "tmp" + Path.DirectorySeparatorChar + "sub").FullName;
            var localFilePath2tmp = TestUtils.CopyAsTempTestFile("MID/villg.mid");
            localFilePath2 = Path.Combine(testSubfolder, "villg.mid");
            File.Move(localFilePath2tmp, localFilePath2, true);
        }

        [AssemblyCleanup]
        public static void ClassCleanup()
        {
            if (Settings.DeleteAfterSuccess)
            {
                foreach (var path in pathsToWrite)
                {
                    if (!path.StartsWith("http")) File.Delete(path);
                }

                if (localFilePath1 != null) File.Delete(localFilePath1);
                if (localFilePath2 != null) File.Delete(localFilePath2);
                if (testSubfolder != null) Directory.Delete(testSubfolder);
            }
        }

        public void PLIO_R(string fileName, string pathReplacement, int nbTracks)
        {
            var replacements = new List<KeyValuePair<string, string>>
            {
                new("$PATH", pathReplacement)
            };
            PLIO_R(fileName, replacements, nbTracks);
        }

        public void PLIO_R(string fileName, IList<KeyValuePair<string, string>> pathReplacements, int nbTracks)
        {
            var testFileLocation = TestUtils.CopyFileAndReplace(TestUtils.GetResourceLocationRoot() + "_Playlists/" + fileName, pathReplacements);
            try
            {
                var theReader = PlaylistIOFactory.GetInstance().GetPlaylistIO(testFileLocation);
                var filePaths = theReader.FilePaths;
                bool foundHttp = false;

                Assert.IsNotInstanceOfType(theReader, typeof(ATL.Playlist.IO.DummyIO));
                Assert.AreEqual(nbTracks, filePaths.Count);
                foreach (string s in theReader.FilePaths)
                {
                    if (!s.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(File.Exists(s), s);
                    else foundHttp = true;
                }
                Assert.IsTrue(foundHttp);
                foreach (Track t in theReader.Tracks)
                {
                    // Ensure the track has been parsed when it points to a file
                    if (!t.Path.StartsWith("http", StringComparison.InvariantCultureIgnoreCase)) Assert.IsTrue(t.Duration > 0);
                }
            }
            finally
            {
                if (Settings.DeleteAfterSuccess) File.Delete(testFileLocation);
            }
        }

        public string PLIO_RW_Absolute_Relative_Path(string extension)
        {
            // == RESOURCE PREPARATION
            // Create new playlist file on temp folder
            var playlistPath = TestUtils.CreateTempTestFile("playlist." + extension);

            // == TEST
            var initialSetting = ATL.Settings.PlaylistWriteAbsolutePath;
            try
            {
                // Add 1 local files and 1 remote files to playlist using absolute paths
                ATL.Settings.PlaylistWriteAbsolutePath = true;
                var playlistIO = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);
                playlistIO.FilePaths.Add(remoteFilePath1);
                playlistIO.FilePaths.Add(localFilePath1);
                playlistIO.Save();

                // Add the other local file using relative path
                ATL.Settings.PlaylistWriteAbsolutePath = false;
                playlistIO = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);
                playlistIO.FilePaths.Add(localFilePath2);
                playlistIO.Save();

                // Change the title of track 2
                playlistIO = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);
                playlistIO.Tracks[1].Title = NEW_TITLE;
                playlistIO.Save();

                // Add the final file using absolute path
                ATL.Settings.PlaylistWriteAbsolutePath = true;
                playlistIO = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);
                playlistIO.FilePaths.Add(remoteFilePath2);
                playlistIO.Save();

                playlistIO = PlaylistIOFactory.GetInstance().GetPlaylistIO(playlistPath);
                foreach (var track in playlistIO.Tracks) Assert.IsTrue(track.Duration > 0);
            }
            finally
            {
                ATL.Settings.PlaylistWriteAbsolutePath = initialSetting;
            }

            return playlistPath;
        }
    }
}