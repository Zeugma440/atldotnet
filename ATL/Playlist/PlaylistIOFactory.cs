using ATL.Playlist.IO;
using System.Collections.Generic;

namespace ATL.Playlist
{
    /// <summary>
    /// Factory for playlist I/O classes
    /// </summary>
    public class PlaylistIOFactory : Factory
    {
        // Supported playilst formats
        /// <summary>
        /// M3U format
        /// </summary>
        public const int PL_M3U = 0;
        /// <summary>
        /// PLS format
        /// </summary>
        public const int PL_PLS = 1;
        /// <summary>
        /// FPL format
        /// </summary>
        public const int PL_FPL = 2;
        /// <summary>
        /// XSPF format
        /// </summary>
        public const int PL_XSPF = 3;
        /// <summary>
        /// SMIL format
        /// </summary>
        public const int PL_SMIL = 4;
        /// <summary>
        /// ASX format
        /// </summary>
        public const int PL_ASX = 5;
        /// <summary>
        /// B4S format
        /// </summary>
        public const int PL_B4S = 6;
        /// <summary>
        /// Daum Playlist (PotPlayer)
        /// </summary>
        public const int PL_DPL = 7;

        // The instance of this factory
        private static PlaylistIOFactory theFactory;

        private static readonly object _lockable = new object();

        /// <summary>
        /// Get an instance of the factory
        /// </summary>
        /// <returns>Instance of the playlist factory</returns>
        public static PlaylistIOFactory GetInstance()
        {
            lock (_lockable)
            {
                if (null == theFactory)
                {
                    theFactory = new PlaylistIOFactory();
                    theFactory.formatListByExt = new Dictionary<string, IList<Format>>();

                    PlaylistFormat tempFmt = new PlaylistFormat(PL_M3U, "M3U");
                    tempFmt.AddExtension(".m3u");
                    tempFmt.AddExtension(".m3u8");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_PLS, "PLS");
                    tempFmt.AddExtension(".pls");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_FPL, "FPL (experimental)", false);
                    tempFmt.LocationFormat = PlaylistFormat.LocationFormatting.MS_URI;
                    tempFmt.AddExtension(".fpl");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_XSPF, "XSPF (spiff)");
                    tempFmt.AddExtension(".xspf");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_SMIL, "SMIL");
                    tempFmt.LocationFormat = PlaylistFormat.LocationFormatting.RFC_URI;
                    tempFmt.AddExtension(".smil");
                    tempFmt.AddExtension(".smi");
                    tempFmt.AddExtension(".zpl");
                    tempFmt.AddExtension(".wpl");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_ASX, "ASX");
                    tempFmt.LocationFormat = PlaylistFormat.LocationFormatting.FilePath;
                    tempFmt.AddExtension(".asx");
                    tempFmt.AddExtension(".wax");
                    tempFmt.AddExtension(".wvx");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_B4S, "B4S");
                    tempFmt.Encoding = PlaylistFormat.FileEncoding.UTF8_NO_BOM;
                    tempFmt.LocationFormat = PlaylistFormat.LocationFormatting.RFC_URI;
                    tempFmt.AddExtension(".b4s");
                    theFactory.addFormat(tempFmt);

                    tempFmt = new PlaylistFormat(PL_DPL, "DPL");
                    tempFmt.Encoding = PlaylistFormat.FileEncoding.UTF8_BOM;
                    tempFmt.LocationFormat = PlaylistFormat.LocationFormatting.FilePath;
                    tempFmt.AddExtension(".dpl");
                    theFactory.addFormat(tempFmt);
                }
            }

            return theFactory;
        }

        /// <summary>
        /// Create a new playlist management object from the given parameters
        /// </summary>
        /// <param name="path">Path of the playlist file to open</param>
        /// <param name="locationFormatting">Formatting of paths within the playlist</param>
        /// <param name="fileEncoding">Encoding of the file</param>
        /// <param name="alternate">Internal use; should be zero when called from outside</param>
        /// <returns></returns>
        public IPlaylistIO GetPlaylistIO(
            string path,
            PlaylistFormat.LocationFormatting locationFormatting = PlaylistFormat.LocationFormatting.Undefined,
            PlaylistFormat.FileEncoding fileEncoding = PlaylistFormat.FileEncoding.Undefined,
            int alternate = 0)
        {
            IList<Format> formats = (List<Format>)getFormatsFromPath(path);
            Format format;
            IPlaylistIO result;

            if (formats != null && formats.Count > alternate)
            {
                format = formats[alternate];
            }
            else
            {
                format = UNKNOWN_FORMAT;
            }
            result = GetPlaylistIO(format.ID);
            result.Path = path;
            // Default settings inherited from format
            if (!format.Equals(UNKNOWN_FORMAT))
            {
                result.LocationFormatting = locationFormatting == PlaylistFormat.LocationFormatting.Undefined ? ((PlaylistFormat)format).LocationFormat : locationFormatting;
                result.Encoding = fileEncoding == PlaylistFormat.FileEncoding.Undefined ? ((PlaylistFormat)format).Encoding : fileEncoding;
            }

            return result;
        }

        /// <summary>
        /// Create a new playlist management object from the given playlist format code (see public constants in PlaylistIOFactory)
        /// </summary>
        /// <param name="formatId">Playlist format code of the object to create</param>
        /// <returns>New playlist management object correspondingf to the given code</returns>
        public IPlaylistIO GetPlaylistIO(int formatId)
        {
            IPlaylistIO theReader = null;

            if (PL_M3U == formatId)
            {
                theReader = new M3UIO();
            }
            else if (PL_PLS == formatId)
            {
                theReader = new PLSIO();
            }
            else if (PL_FPL == formatId)
            {
                theReader = new FPLIO();
            }
            else if (PL_XSPF == formatId)
            {
                theReader = new XSPFIO();
            }
            else if (PL_SMIL == formatId)
            {
                theReader = new SMILIO();
            }
            else if (PL_ASX == formatId)
            {
                theReader = new ASXIO();
            }
            else if (PL_B4S == formatId)
            {
                theReader = new B4SIO();
            }
            else if (PL_DPL == formatId)
            {
                theReader = new DPLIO();
            }

            if (null == theReader) theReader = new DummyIO();

            return theReader;
        }
    }
}
