using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ATL.AudioData;

namespace ATL.test.IO.MetaData
{
    [TestClass]
    public class ID3v1
    {
        [TestMethod]
        public void ID3v1ReadWrite()
        {
            String location = "../../Resources/empty.mp3";

            IAudioDataIO theFile = AudioData.AudioDataIOFactory.GetInstance().GetDataReader(location);

            if (theFile.ReadFromFile())
            {
                Assert.IsNotNull(theFile.ID3v1);
                Assert.IsFalse(theFile.ID3v1.Exists);
            }
            else
            {
                Assert.Fail();
            }

            TagData theTag = new TagData();
            theTag.Title = "test !!";

            if (theFile.AddTagToFile(theTag, MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v1);
                    Assert.IsTrue(theFile.ID3v1.Exists);
                    Assert.AreEqual(theFile.ID3v1.Title, "test !!");
                }
                else
                {
                    Assert.Fail();
                }
            }
            else
            {
                Assert.Fail();
            }

            if (theFile.RemoveTagFromFile(MetaDataIOFactory.TAG_ID3V1))
            {
                if (theFile.ReadFromFile())
                {
                    Assert.IsNotNull(theFile.ID3v1);
                    Assert.IsFalse(theFile.ID3v1.Exists);
                }
                else
                {
                    Assert.Fail();
                }
            }
            else
            {
                Assert.Fail();
            }

        }
    }
}
