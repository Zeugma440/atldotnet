using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;
using System.IO;
using System.Drawing;
using System.Collections.Generic;

namespace ATL.test.IO
{
    // Class dedicated to maintaining test coverage for obsolete methods (yes, they are obsolete but they still belong to "living" code !)
    [TestClass]
    public class Obsolete
    {
        protected class PictureInfo
        {
            public ATL.PictureInfo info;
            public Image Picture;

            public PictureInfo(byte[] pictureData, Commons.ImageFormat imgFormat, object code)
            {
                info = new ATL.PictureInfo(imgFormat, MetaDataIOFactory.TAG_ANY, code);
                info.PictureData = pictureData;
                info.PictureHash = HashDepot.Fnv1a.Hash32(info.PictureData);
                Picture = Image.FromStream(new MemoryStream(pictureData));
            }

            // Retrocompatibility with old interface
            public int PictureCodeInt
            {
                get { return info.NativePicCode; }
            }
            public string PictureCodeStr
            {
                get { return info.NativePicCodeStr; }
            }
        }

        protected IList<KeyValuePair<ATL.PictureInfo.PIC_TYPE, PictureInfo>> pictures = new List<KeyValuePair<ATL.PictureInfo.PIC_TYPE, PictureInfo>>();


        protected void readPictureData(ref MemoryStream s, ATL.PictureInfo.PIC_TYPE picType, Commons.ImageFormat imgFormat, int originalTag, object picCode, int position)
        {
            pictures.Add(new KeyValuePair<ATL.PictureInfo.PIC_TYPE, PictureInfo>(picType, new PictureInfo(s.ToArray(), imgFormat, picCode)));
        }

        [TestMethod]
        public void AudioDataManager_ReadFromFile()
        {
            string location = TestUtils.GetResourceLocationRoot() + "MP3/ID3v2.2 ANSI charset only.mp3";
            AudioDataManager theFile = new AudioDataManager(AudioData.AudioDataIOFactory.GetInstance().GetFromPath(location));

            pictures.Clear();
            Assert.IsTrue(theFile.ReadFromFile(new TagData.PictureStreamHandlerDelegate(this.readPictureData), true));

            Assert.IsNotNull(theFile.ID3v2);
            Assert.IsTrue(theFile.ID3v2.Exists);

            // Supported fields
            Assert.AreEqual("noTagnoTag", theFile.ID3v2.Title);
            Assert.AreEqual("ALBUM!", theFile.ID3v2.Album);
            Assert.AreEqual("ARTIST", theFile.ID3v2.Artist);
            Assert.AreEqual("ALBUMARTIST", theFile.ID3v2.AlbumArtist);
            Assert.AreEqual("I have no IDE and i must code", theFile.ID3v2.Comment);
            Assert.AreEqual("1997", theFile.ID3v2.Year);
            Assert.AreEqual("House", theFile.ID3v2.Genre);
            Assert.AreEqual(1, theFile.ID3v2.Track);
            Assert.AreEqual("COMP!", theFile.ID3v2.Composer);
            Assert.AreEqual(2, theFile.ID3v2.Disc);

            // Pictures
            Assert.AreEqual(1, pictures.Count);
            byte found = 0;

            foreach (KeyValuePair<ATL.PictureInfo.PIC_TYPE, PictureInfo> pic in pictures)
            {
                Image picture;
                if (pic.Key.Equals(ATL.PictureInfo.PIC_TYPE.Generic)) // Supported picture
                {
                    picture = pic.Value.Picture;
                    Assert.AreEqual(picture.RawFormat, System.Drawing.Imaging.ImageFormat.Jpeg);
                    Assert.AreEqual(picture.Height, 656);
                    Assert.AreEqual(picture.Width, 552);
                    found++;
                }
            }

            Assert.AreEqual(1, found);
        }

    }
}
