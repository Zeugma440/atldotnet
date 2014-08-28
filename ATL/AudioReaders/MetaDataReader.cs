using ATL.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ATL.AudioReaders
{
    abstract class MetaDataReader : IMetaDataReader
    {
        abstract public bool Exists
        {
            get;
        }
        abstract public String Title
        {
            get;
        }
        abstract public String Artist
        {
            get;
        }
        abstract public String Composer
        {
            get;
        }
        abstract public String Comment
        {
            get;
        }
        abstract public String Genre
        {
            get;
        }
        abstract public ushort Track
        {
            get;
        }
        abstract public ushort Disc
        {
            get;
        }
        abstract public String Year
        {
            get;
        }
        abstract public String Album
        {
            get;
        }
        abstract public ushort Rating
        {
            get;
        }
        abstract public IList<MetaReaderFactory.PIC_CODE> Pictures
        {
            get;
        }

        abstract protected void ResetData();

        abstract public bool ReadFromFile(BinaryReader Source, StreamUtils.StreamHandlerDelegate pictureStreamHandler);

        public bool ReadFromFile(String FileName, StreamUtils.StreamHandlerDelegate pictureStreamHandler)
        {
            FileStream fs = null;
            BinaryReader source = null;

            bool result = false;
            ResetData();

            try
            {
                // Open file, read first block of data and search for a frame		  
                fs = new FileStream(FileName, FileMode.Open, FileAccess.Read);
                source = new BinaryReader(fs);

                return ReadFromFile(source, pictureStreamHandler);
            }
            catch (Exception e)
            {
                System.Console.WriteLine(e.StackTrace);
                LogDelegator.GetLogDelegate()(Log.LV_ERROR, e.Message + " (" + FileName + ")");
                result = false;
            }

            if (source != null) source.Close();
            if (fs != null) fs.Close();

            return result;
        }
    }
}
