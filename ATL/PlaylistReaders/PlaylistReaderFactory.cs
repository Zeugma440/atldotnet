using System;
using System.IO;
using ATL.PlaylistReaders.BinaryLogic;
using System.Collections.Generic;

namespace ATL.PlaylistReaders
{
    /// <summary>
    /// Description r¨¦sum¨¦e de PlaylistReaderFactory.
    /// </summary>
    public class PlaylistReaderFactory : ReaderFactory
    {
        // The instance of this factory
        private static PlaylistReaderFactory theFactory = null;

        public static PlaylistReaderFactory GetInstance()
        {
            if (null == theFactory)
            {
                theFactory = new PlaylistReaderFactory
                {
                    formatListByExt = new Dictionary<string, IList<Format>>()
                };
                theFactory.addFormat(PLSFormat());
                theFactory.addFormat(M3UFormat());
                theFactory.addFormat(FPLFFormat());
                theFactory.addFormat(XSPFFormat());
                theFactory.addFormat(SMILFormat());
                theFactory.addFormat(ASXFormat());
                theFactory.addFormat(B4SFormat());
            }

            return theFactory;
        }

        private static Format PLSFormat()
        {
            Format format = new Format("PLS")
            {
                ID = (int)EPlayListFormats.PL_PLS
            };
            format.AddExtensions(".pls");
            return format;
        }
        private static Format M3UFormat()
        {
            var format = new Format("M3U")
            {
                ID = (int)EPlayListFormats.PL_M3U
            };
            format.AddExtensions(".m3u", ".m3u8");
            return format;
        }
        private static Format FPLFFormat()
        {
            Format format = new Format("FPL (experimental)")
            {
                ID = (int)EPlayListFormats.PL_FPL
            };
            format.AddExtensions(".fpl");
            return format;
        }
        private static Format XSPFFormat()
        {
            Format format = new Format("XSPF(spiff)")
            {
                ID = (int)EPlayListFormats.PL_XSPF
            };
            format.AddExtensions(".xspf");
            return format;
        }
        private static Format SMILFormat()
        {
            var format = new Format("SMIL")
            {
                ID = (int)EPlayListFormats.PL_SMIL
            };
            format.AddExtensions(".smil", ".smi", ".zpl", ".wpl");
            return format;
        }
        private static Format ASXFormat()
        {
            Format format = new Format("ASX")
            {
                ID = (int)EPlayListFormats.PL_ASX
            };
            format.AddExtensions(".asx", ".wax", ".wvx");
            return format;
        }
        private static Format B4SFormat()
        {
            Format format = new Format("B4S")
            {
                ID = (int)EPlayListFormats.PL_B4S
            };
            format.AddExtensions(".b4s");
            return format;
        }

        public IPlaylistReader GetPlaylistReader(String path, int alternate = 0)
        {
            IList<Format> formats = getFormatsFromPath(path);
            IPlaylistReader result;

            if (formats != null && formats.Count > alternate)
            {
                result = GetPlaylistReader(formats[alternate].ID);
            }
            else
            {
                result = GetPlaylistReader(NO_FORMAT);
            }

            result.Path = path;
            return result;
        }

        public IPlaylistReader GetPlaylistReader(int formatId)
        {
            switch ((EPlayListFormats)formatId)
            {
                case EPlayListFormats.PL_M3U: return new M3UReader();
                case EPlayListFormats.PL_PLS: return new PLSReader();
                case EPlayListFormats.PL_FPL: return new FPLReader();
                case EPlayListFormats.PL_XSPF: return new XSPFReader();
                case EPlayListFormats.PL_SMIL: return new SMILReader();
                case EPlayListFormats.PL_ASX: return new ASXReader();
                case EPlayListFormats.PL_B4S: return new B4SReader();
                default: return new DummyReader(); //TODO better to throw Exception.
            }
        }
    }
}
