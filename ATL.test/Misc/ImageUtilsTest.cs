using Microsoft.VisualStudio.TestTools.UnitTesting;
using Commons;

namespace ATL.test
{
    [TestClass]
    public class ImageUtilsTest
    {
/*
        [TestMethod]
        public void Utils_ResizePic()
        {
            Image image = Image.FromFile(TestUtils.GetResourceLocationRoot() + "pic1.jpg");
            Assert.AreEqual(900, image.Width);
            Assert.AreEqual(600, image.Height);

            Image resizedImage = Utils.ResizeImage(image, new Size(50, 50), false);
            Assert.AreEqual(50, resizedImage.Width);
            Assert.AreEqual(50, resizedImage.Height);

            resizedImage = Utils.ResizeImage(image, new Size(50, 50), true);
            Assert.AreEqual(50, resizedImage.Width);
            Assert.AreEqual(33, resizedImage.Height);
        }
*/

        [TestMethod]
        public void ImgUtils_LoadJpeg()
        {
            // THis one has multiple image data segments; never figured out why
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.jpg");
            ImageProperties props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(900, props.Width);
            Assert.AreEqual(600, props.Height);
            Assert.AreEqual(0, props.NumColorsInPalette);
            Assert.AreEqual(24, props.ColorDepth);

            // This one is plain and simple
            data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic2.jpg");
            props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(900, props.Width);
            Assert.AreEqual(290, props.Height);
            Assert.AreEqual(0, props.NumColorsInPalette);
            Assert.AreEqual(24, props.ColorDepth);
        }

        [TestMethod]
        public void ImgUtils_LoadPng()
        {
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.png");
            ImageProperties props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(175, props.Width);
            Assert.AreEqual(168, props.Height);
            Assert.AreEqual(15, props.NumColorsInPalette);
            Assert.AreEqual(8, props.ColorDepth);
        }

        [TestMethod]
        public void ImgUtils_LoadBmp()
        {
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.bmp");
            ImageProperties props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(175, props.Width);
            Assert.AreEqual(168, props.Height);
            Assert.AreEqual(0, props.NumColorsInPalette);
            Assert.AreEqual(8, props.ColorDepth);
        }

        [TestMethod]
        public void ImgUtils_LoadGif()
        {
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/pic1.gif");
            ImageProperties props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(175, props.Width);
            Assert.AreEqual(168, props.Height);
            Assert.AreEqual(256, props.NumColorsInPalette);
            Assert.AreEqual(24, props.ColorDepth);
        }

        [TestMethod]
        public void ImgUtils_LoadTiff()
        {
            byte[] data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/bilevel.tif");
            ImageProperties props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(1728, props.Width);
            Assert.AreEqual(2376, props.Height);
            Assert.AreEqual(0, props.NumColorsInPalette);
            Assert.AreEqual(1, props.ColorDepth);

            data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/rgb.tif");
            props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(124, props.Width);
            Assert.AreEqual(124, props.Height);
            Assert.AreEqual(0, props.NumColorsInPalette);
            Assert.AreEqual(24, props.ColorDepth);

            data = System.IO.File.ReadAllBytes(TestUtils.GetResourceLocationRoot() + "_Images/palette.tif");
            props = ImageUtils.GetImageProperties(data);

            Assert.AreEqual(2147, props.Width);
            Assert.AreEqual(1027, props.Height);
            Assert.AreEqual(8, props.NumColorsInPalette);
            Assert.AreEqual(8, props.ColorDepth);
        }
    }
}
