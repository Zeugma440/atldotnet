using ATL.Playlist.IO;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace ATL.Playlist
{
    /// <summary>
    /// Factory for playlist I/O classes
    /// </summary>
    public class PlaylistIOFactory : Factory<Format>
    {
        // Supported playilst formats
        /// <summary>
        /// M3U format
        /// </summary>
        public const int PL_M3U = 1;
        /// <summary>
        /// PLS format
        /// </summary>
        public const int PL_PLS = 2;
        /// <summary>
        /// FPL format
        /// </summary>
        public const int PL_FPL = 3;
        /// <summary>
        /// XSPF format
        /// </summary>
        public const int PL_XSPF = 4;
        /// <summary>
        /// SMIL format
        /// </summary>
        public const int PL_SMIL = 5;
        /// <summary>
        /// ASX format
        /// </summary>
        public const int PL_ASX = 6;
        /// <summary>
        /// B4S format
        /// </summary>
        public const int PL_B4S = 7;
        /// <summary>
        /// Daum Playlist (PotPlayer)
        /// </summary>
        public const int PL_DPL = 8;

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
                if (null != theFactory) return theFactory;

                theFactory = new PlaylistIOFactory
                {
                    formatListByExt = new Dictionary<string, IList<Format>>()
                };

                PlaylistFormat tempFmt = new PlaylistFormat(PL_M3U, "M3U");
                tempFmt.AddExtension(".m3u");
                tempFmt.AddExtension(".m3u8");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_PLS, "PLS");
                tempFmt.AddExtension(".pls");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_FPL, "FPL (experimental)", false)
                {
                    LocationFormat = PlaylistFormat.LocationFormatting.MS_URI
                };
                tempFmt.AddExtension(".fpl");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_XSPF, "XSPF (spiff)");
                tempFmt.AddExtension(".xspf");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_SMIL, "SMIL")
                {
                    LocationFormat = PlaylistFormat.LocationFormatting.RFC_URI
                };
                tempFmt.AddExtension(".smil");
                tempFmt.AddExtension(".smi");
                tempFmt.AddExtension(".zpl");
                tempFmt.AddExtension(".wpl");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_ASX, "ASX")
                {
                    LocationFormat = PlaylistFormat.LocationFormatting.FilePath
                };
                tempFmt.AddExtension(".asx");
                tempFmt.AddExtension(".wax");
                tempFmt.AddExtension(".wvx");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_B4S, "B4S")
                {
                    Encoding = PlaylistFormat.FileEncoding.UTF8_NO_BOM,
                    LocationFormat = PlaylistFormat.LocationFormatting.RFC_URI
                };
                tempFmt.AddExtension(".b4s");
                theFactory.addFormat(tempFmt);

                tempFmt = new PlaylistFormat(PL_DPL, "DPL")
                {
                    Encoding = PlaylistFormat.FileEncoding.UTF8_BOM,
                    LocationFormat = PlaylistFormat.LocationFormatting.FilePath
                };
                tempFmt.AddExtension(".dpl");
                theFactory.addFormat(tempFmt);
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
        [SuppressMessage("ReSharper", "UnusedMember.Global")]
        public IPlaylistIO GetPlaylistIO(
            string path,
            PlaylistFormat.LocationFormatting locationFormatting = PlaylistFormat.LocationFormatting.Undefined,
            PlaylistFormat.FileEncoding fileEncoding = PlaylistFormat.FileEncoding.Undefined,
            int alternate = 0)
        {
            IList<Format> formats = (List<Format>)getFormatsFromPath(path);
            Format format;

            if (formats != null && formats.Count > alternate)
            {
                format = formats[alternate];
            }
            else
            {
                format = Format.UNKNOWN_FORMAT;
            }
            var result = GetPlaylistIO(format.ID, path);

            // Default settings inherited from format
            if (!format.Equals(Format.UNKNOWN_FORMAT))
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
        /// <param name="path">Path of the playlist file to use</param>
        /// <returns>New playlist management object correspondingf to the given code</returns>
        public static IPlaylistIO GetPlaylistIO(int formatId, string path)
        {
            IPlaylistIO theReader = formatId switch
            {
                PL_M3U => new M3UIO(path),
                PL_PLS => new PLSIO(path),
                PL_FPL => new FPLIO(path),
                PL_XSPF => new XSPFIO(path),
                PL_SMIL => new SMILIO(path),
                PL_ASX => new ASXIO(path),
                PL_B4S => new B4SIO(path),
                PL_DPL => new DPLIO(path),
                _ => null
            };

            return theReader ?? new DummyIO();
        }
    }
}
