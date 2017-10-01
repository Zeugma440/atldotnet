using ATL;
using ATL.Logging;
using System;
using System.IO;

namespace Commons
{
    public enum ImageFormat { Unsupported = 99, Undefined = 98, Jpeg = 1, Gif = 2, Png = 3, Bmp = 4, Tiff = 5 };

    public class ImageProperties
    {
        public ImageFormat Format = ImageFormat.Undefined;
        public int Width = 0;
        public int Height = 0;
        public int ColorDepth = 0;
        public int NumColorsInPalette = 0;
    }

    public static class ImageUtils
    {
        public static BinaryReader BinaryReader { get; private set; }

        /// <summary>
        /// Returns the mime-type of the given .NET image format
        /// NB : This function is restricted to most common embedded picture formats : JPEG, GIF, PNG, BMP
        /// </summary>
        /// <param name="imageFormat">Image format whose mime-type to obtain</param>
        /// <returns>mime-type of the given image format</returns>
        public static string GetMimeTypeFromImageFormat(ImageFormat imageFormat)
        {
            string result = "image/";

            if (imageFormat.Equals(ImageFormat.Jpeg))
            {
                result += "jpeg";
            }
            else if (imageFormat.Equals(ImageFormat.Gif))
            {
                result += "gif";
            }
            else if (imageFormat.Equals(ImageFormat.Png))
            {
                result += "png";
            }
            else if (imageFormat.Equals(ImageFormat.Bmp))
            {
                result += "bmp";
            }
            else if (imageFormat.Equals(ImageFormat.Tiff))
            {
                result += "tiff";
            }

            return result;
        }

        /// <summary>
        /// Returns the .NET image format of the given mime-type
        /// NB1 : This function is restricted to most common embedded picture formats : JPEG, GIF, PNG, BMP
        /// NB2 : This function does not verify the syntax of the mime-type (e.g. "image/XXX"), and only focuses on the presence of specific substrings (e.g. "gif")
        /// </summary>
        /// <param name="mimeType">Mime-type whose ImageFormat to obtain</param>
        /// <returns>ImageFormat of the given mime-type (default : JPEG)</returns>
        public static ImageFormat GetImageFormatFromMimeType(string mimeType)
        {
            ImageFormat result = ImageFormat.Jpeg;

            if (mimeType.Contains("gif"))
            {
                result = ImageFormat.Gif;
            }
            else if (mimeType.Contains("png"))
            {
                result = ImageFormat.Png;
            }
            else if (mimeType.Contains("bmp"))
            {
                result = ImageFormat.Bmp;
            }
            else if (mimeType.Contains("tiff"))
            {
                result = ImageFormat.Tiff;
            }

            return result;
        }

        /// <summary>
        /// Resizes the given image to the given dimensions
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="size">Target dimensions</param>
        /// <param name="preserveAspectRatio">True if the resized image has to keep the same aspect ratio as the given image; false if not (optional; default value = true)</param>
        /// <returns>Resized image</returns>
        /*
         * Requires an external lib compatible with .net Core
         * 
        public static Image ResizeImage(Image image, Size size, bool preserveAspectRatio = true)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size.Width / originalWidth;
                float percentHeight = (float)size.Height / originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = Convert.ToInt32(Math.Round(originalWidth * percent, 0));
                newHeight = Convert.ToInt32(Math.Round(originalHeight * percent, 0));
            }
            else
            {
                newWidth = size.Width;
                newHeight = size.Height;
            }
            Image newImage = new Bitmap(newWidth, newHeight);
            using (Graphics graphicsHandle = Graphics.FromImage(newImage))
            {
                graphicsHandle.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphicsHandle.DrawImage(image, 0, 0, newWidth, newHeight);
            }
            return newImage;
        }
        */

        /// <summary>
        /// Detects image format from the given signature
        /// </summary>
        /// <param name="header">Binary signature; must be at least 3-bytes long</param>
        /// <returns>Detected image format corresponding to the given signature; null if no match is found</returns>
        public static ImageFormat GetImageFormatFromPictureHeader(byte[] header)
        {
            if (header.Length < 3) throw new FormatException("Header length must be at least 3");

            if (0xFF == header[0] && 0xD8 == header[1] && 0xFF == header[2]) return ImageFormat.Jpeg;
            else if (0x42 == header[0] && 0x4D == header[1]) return ImageFormat.Bmp;
            else if (0x47 == header[0] && 0x49 == header[1] && 0x46 == header[2]) return ImageFormat.Gif;
            else if (0x89 == header[0] && 0x50 == header[1] && 0x4E == header[2]) return ImageFormat.Png;
            else if (0x49 == header[0] && 0x49 == header[1] && 0x2A == header[2]) return ImageFormat.Tiff; // Little Endian TIFF
            else if (0x4D == header[0] && 0x4D == header[1] && 0x00 == header[2]) return ImageFormat.Tiff; // Big Endian TIFF
            else return ImageFormat.Unsupported;
        }

        public static ImageProperties GetImageProperties(byte[] imageData, ImageFormat format = ImageFormat.Undefined)
        {
            ImageProperties props = new ImageProperties();

            if (ImageFormat.Undefined.Equals(format)) format = GetImageFormatFromPictureHeader(imageData);

            if (format.Equals(ImageFormat.Unsupported)) return props;

            props.NumColorsInPalette = 0;
            props.Format = format;

            using (MemoryStream s = new MemoryStream(imageData))
            using (BinaryReader r = new BinaryReader(s))
            {
                long limit = (long)Math.Round(s.Length * 0.25); // TODO - test and adjust limit

                switch (format)
                {
                    case (ImageFormat.Png):
                        byte[] intData = new byte[4];
                        byte[] IHDRChunkSignature = Utils.Latin1Encoding.GetBytes("IHDR");
                        byte[] PaletteChunkSignature = Utils.Latin1Encoding.GetBytes("PLTE");

                        // Skip header
                        s.Seek(8, SeekOrigin.Begin);

                        // Scroll chunks until we find IHDR (that should be the first one to appear, but who knows...)
                        if (0 == findPngChunk(s, IHDRChunkSignature, limit))
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Invalid PNG file; no IHDR chunk found");
                        }
                        else
                        {
                            // Read IHDR chunk
                            s.Read(intData, 0, 4);
                            props.Width = StreamUtils.DecodeBEInt32(intData);
                            s.Read(intData, 0, 4);
                            props.Height = StreamUtils.DecodeBEInt32(intData);
                            props.ColorDepth = r.ReadByte();
                            int colorType = r.ReadByte();
                            if (3 == colorType) // PNG file uses a palette
                            {
                                s.Seek(7, SeekOrigin.Current); // 3 last useful data + ending chunk CRC
                                uint paletteChunkSize = findPngChunk(s, PaletteChunkSignature, limit);
                                if (0 == paletteChunkSize)
                                {
                                    LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Invalid PNG file; palette declared, but no PLTE chunk found");
                                } else
                                {
                                    props.NumColorsInPalette = (int)Math.Floor(paletteChunkSize / 3.0);
                                }
                            }
                            else
                            {
                                props.NumColorsInPalette = 0;
                            }
                        }

                        break;
                    case (ImageFormat.Jpeg):
                        byte[] shortData = new byte[2];
                        byte[] SOF0FrameSignature = new byte[2] { 0xFF, 0xC0 };

                        /*
                         * We just need to reach the SOF0 frame descripting the actual picture
                         * 
                         * In order to handle JPEG files that contain multiple SOF0 frames (see test suite),
                         * the simplest way of proceeding is to look for all SOF0 frames in the first 25% of the file,
                         * and then read the very last one
                        */
                        long lastPos = 0;

                        while (StreamUtils.FindSequence(s, SOF0FrameSignature, limit))
                        {
                            lastPos = s.Position;
                        }

                        if (0 == lastPos)
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Invalid JPEG file; no SOF0 frame found");
                        } 
                        else
                        {
                            // Skip frame length
                            s.Seek(2, SeekOrigin.Current);
                            byte bitsPerSample = r.ReadByte();
                            s.Read(shortData, 0, 2);
                            props.Height = StreamUtils.DecodeBEUInt16(shortData);
                            s.Read(shortData, 0, 2);
                            props.Width = StreamUtils.DecodeBEUInt16(shortData);
                            byte nbComponents = r.ReadByte();
                            props.ColorDepth = bitsPerSample * nbComponents;
                        }

                        /*
                        // Skip JPEG signature
                        s.Seek(2, SeekOrigin.Current);

                        s.Read(frameMarker, 0, 2);
                        // Skip the APP0 field
                        if (0xFF == frameMarker[0] && 0xE0 == frameMarker[1])
                        {
                            frameLength = r.ReadInt16();
                            s.Seek(frameLength, SeekOrigin.Current);
                        }

                        // Skip APPn fields, if existing
                        s.Read(frameMarker, 0, 2);
                        while (0xFF == frameMarker[0] && 0xE1 <= frameMarker[1] && 0xEF >= frameMarker[1] )
                        {
                            frameLength = r.ReadInt16();
                            s.Seek(frameLength, SeekOrigin.Current);
                            s.Read(frameMarker, 0, 2);
                        }

                        // Skip DQT frames
                        while (0xFF == frameMarker[0] && 0xDB == frameMarker[1])
                        {
                            frameLength = r.ReadInt16();
                            s.Seek(frameLength + 1, SeekOrigin.Current);
                            s.Read(frameMarker, 0, 2);
                        }

                        // Read SOF0 frame (at last...)
                        if (0xFF == frameMarker[0] && 0xC0 == frameMarker[1])
                        {
                            frameLength = r.ReadInt16();
                            byte bitsPerSample = r.ReadByte();
                            props.Height = r.ReadInt16();
                            props.Width = r.ReadInt16();
                            byte nbComponents = r.ReadByte();
                            props.ColorDepth = bitsPerSample * nbComponents;
                        }*/

                        break;
                }
            }

            return props;
        }

        private static uint findPngChunk(Stream s, byte[] chunkID, long limit)
        {
            byte[] intData = new byte[4];
            uint chunkSize;
            bool foundChunk = false;

            while (s.Position < limit)
            {
                s.Read(intData, 0, 4); // Chunk Size
                chunkSize = StreamUtils.DecodeBEUInt32(intData);
                s.Read(intData, 0, 4); // Chunk ID
                foundChunk = StreamUtils.ArrEqualsArr(intData, chunkID);
                if (foundChunk)
                {
                    return chunkSize;
                }
                else
                {
                    s.Seek(chunkSize + 4, SeekOrigin.Current);
                }
            }

            return 0;
        }

    }
}
