using ATL.Playlist.IO;
using System.Collections.Generic;

namespace ATL.Playlist
{
    /// <summary>
    /// Factory for playlist I/O classes
    /// </summary>
    public class PlaylistIOFactory : Factory
    {
        // Defines the supported formats
        public const int PL_M3U = 0;
        public const int PL_PLS = 1;
        public const int PL_FPL = 2;
        public const int PL_XSPF = 3;
        public const int PL_SMIL = 4;
        public const int PL_ASX = 5;
        public const int PL_B4S = 6;

        // The instance of this factory
        private static PlaylistIOFactory theFactory = null;


        public static PlaylistIOFactory GetInstance()
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

                tempFmt = new PlaylistFormat(PL_FPL, "FPL (experimental)");
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
            }

            return theFactory;
        }

        public IPlaylistIO GetPlaylistIO(
            string path, 
            PlaylistFormat.LocationFormatting locationFormatting = PlaylistFormat.LocationFormatting.Undefined, 
            PlaylistFormat.FileEncoding fileEncoding = PlaylistFormat.FileEncoding.Undefined,
            int alternate = 0)
        {
            IList<Format> formats = (List<Format>)getFormatsFromPath(path);
            Format format = null;
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
                result.LocationFormatting = (locationFormatting == PlaylistFormat.LocationFormatting.Undefined) ? ((PlaylistFormat)format).LocationFormat : locationFormatting;
                result.Encoding = (fileEncoding == PlaylistFormat.FileEncoding.Undefined) ? ((PlaylistFormat)format).Encoding : fileEncoding;
            }

            return result;
        }

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

            if (null == theReader) theReader = new DummyIO();

            return theReader;
        }
    }
}
