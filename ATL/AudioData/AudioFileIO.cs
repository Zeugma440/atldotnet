using System;
using System.Collections.Generic;
using System.Text;

namespace ATL.AudioData
{
    class AudioFileIO
    {
        public String Title;
        public String Artist;
        public String Composer;
        public String Comment;
        public String Genre;
        public int IntYear;
        public String Album;
        public int Track;
        public int Disc;
        public int IntBitRate;
        public int IntDuration;
        public int CodecFamily;
        public int Rating;
        public bool IsVBR;

        public List<MetaDataIOFactory.PIC_CODE> Pictures;

        public AudioFileIO(String path, MetaDataIOFactory.PictureStreamHandlerDelegate pictureStreamHandler)
        {
            // TODO
        }
    }
}
