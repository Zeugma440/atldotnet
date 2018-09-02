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

    /*
     * Helper methods for reading basic image properties without System.Drawing
     * This class has been created to reach .NET Core 2.0 compatibility
     * 
     * Implementation notes :
     * 
     *  - If a TIFF file has multiple images, only the properties of the 1st image will be read
     *  - BMPs with color palettes are not supported
     * 
     */
    public static class ImageUtils
    {
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
                    case (ImageFormat.Tiff):
                        bool isBigEndian = (0x4D == r.ReadByte());
                        s.Seek(3, SeekOrigin.Current); // Skip the rest of the signature
                        long IFDOffset = readInt32(r, isBigEndian);

                        s.Seek(IFDOffset, SeekOrigin.Begin);

                        int nbIFDEntries = readInt16(r, isBigEndian);

                        long initialPos = s.Position;
                        int IFDtag, IFDFieldType, IFDNbValues, IFDValue32, IFDValue16;
                        byte[] IFDValueBinary;
                        int photometricInterpretation = 0;
                        int bitsPerSample = 0;
                        int samplesPerPixel = 0;

                        for (int i=0; i<nbIFDEntries; i++)
                        {
                            IFDtag = readInt16(r, isBigEndian);
                            IFDFieldType = readInt16(r, isBigEndian);
                            IFDNbValues = readInt32(r, isBigEndian);
                            IFDValueBinary = r.ReadBytes(4);
                            IFDValue32 = isBigEndian? StreamUtils.DecodeBEInt32(IFDValueBinary) : StreamUtils.DecodeInt32(IFDValueBinary);
                            IFDValue16 = isBigEndian ? StreamUtils.DecodeBEInt16(IFDValueBinary) : StreamUtils.DecodeInt16(IFDValueBinary);

                            switch (IFDtag)
                            {
                                // Common properties
                                case (0x0100):
                                    props.Width = IFDValue32;
                                    // Specs say "SHORT or LONG" but the implementation actually takes up 4 bytes anyway -> we'll assume it's a SHORT if the last two bytes are null
                                    if (0 == IFDValueBinary[2] + IFDValueBinary[3]) props.Width = IFDValue16;
                                    break;
                                case (0x0101):
                                    props.Height = IFDValue32;
                                    if (0 == IFDValueBinary[2] + IFDValueBinary[3]) props.Height = IFDValue16;
                                    break;

                                // Specific properties
                                case (0x0106):                  // PhotometricInterpretation
                                    photometricInterpretation = IFDValue32;
                                    if (IFDValue32 < 2) props.ColorDepth = 1;         // Bilevel or greyscale image
                                    else if (2 == IFDValue32) props.ColorDepth = 24;  // RGB full color image
                                    // NB : A value of 3 would indicate a palette-color image, but has no effect here
                                    break;
                                case (0x0102):                  // BitsPerSample
                                    bitsPerSample = IFDValue16;
                                    break;
                                case (0x0115):                  // SamplesPerPixel
                                    samplesPerPixel = IFDValue16;
                                    break;
                            }
                        }

                        if (photometricInterpretation < 2) // Bilevel or greyscale
                        {
                            props.ColorDepth = bitsPerSample;
                        }
                        else if (2 == photometricInterpretation) // RGB
                        {
                            props.ColorDepth = 8 * samplesPerPixel;
                        }
                        else if (3 == photometricInterpretation) // Palette
                        {
                            props.ColorDepth = 8 * samplesPerPixel;
                            props.NumColorsInPalette = bitsPerSample;
                        }


                        break;
                    case (ImageFormat.Gif):
                        byte[] GraphicControlExtensionBlockSignature = new byte[2] { 0x21, 0xf9 };

                        props.ColorDepth = 24; // 1 byte for each component

                        s.Seek(3, SeekOrigin.Current); // Skip GIF signature

                        string version = Utils.Latin1Encoding.GetString(r.ReadBytes(3));

                        s.Seek(4, SeekOrigin.Current); // Skip logical screen descriptors

                        byte globalPaletteUse = r.ReadByte();
                        if (((globalPaletteUse & 0x80) >> 7) > 0) // File uses a global color palette
                        {
                            props.NumColorsInPalette = 2 << (globalPaletteUse & 0x07);
                        }
                        
                        /*
                         * v89a means that the first image block should follow the first graphic control extension block
                         * (which may in turn be located after an application extension block if the GIF is animated)
                         * 
                         * => The simplest way to get to the 1st image block is to look for the 1st 
                         * graphic control extension block, and to skip it
                         */
                        if ("89a".Equals(version))
                        {
                            initialPos = s.Position;
                            if (StreamUtils.FindSequence(s, GraphicControlExtensionBlockSignature))
                            {
                                s.Seek(6, SeekOrigin.Current);
                            } else
                            {
                                LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Invalid v89a GIF file; no graphic control extension block found");
                                // GIF is malformed; trying to find the image block directly
                                s.Seek(initialPos, SeekOrigin.Begin);
                                if (StreamUtils.FindSequence(s, new byte[1] { 0x2c }))
                                {
                                    s.Seek(-1, SeekOrigin.Current);
                                }
                            }
                        }

                        // At this point, we should be at the very beginning of the first image block
                        if (0x2c == r.ReadByte())
                        {
                            s.Seek(4, SeekOrigin.Current); // Skip image position descriptors
                            props.Width = r.ReadInt16();
                            props.Height = r.ReadInt16();
                            
                            // No global palette is set => try and find information in the local palette of the 1st image block
                            if (0 == props.NumColorsInPalette)
                            {
                                props.NumColorsInPalette = (int)Math.Pow(2, ((globalPaletteUse & 0x0F) << 4) + 1);
                            }
                        } else
                        {
                            LogDelegator.GetLogDelegate()(Log.LV_WARNING, "Error parsing GIF file; image block not found");
                        }

                        break;

                    case (ImageFormat.Bmp):

                        // Skip useless information
                        s.Seek(18, SeekOrigin.Begin);

                        props.Width = r.ReadInt32();
                        props.Height = r.ReadInt32();
                        s.Seek(2, SeekOrigin.Current); // Planes
                        props.ColorDepth = r.ReadInt16();

                        // No support for BMP color palettes, as they seem to be exotic (and ATL has no use of this information)

                        break;

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
                            bitsPerSample = r.ReadByte();
                            s.Read(shortData, 0, 2);
                            props.Height = StreamUtils.DecodeBEUInt16(shortData);
                            s.Read(shortData, 0, 2);
                            props.Width = StreamUtils.DecodeBEUInt16(shortData);
                            byte nbComponents = r.ReadByte();
                            props.ColorDepth = bitsPerSample * nbComponents;
                        }

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

        // =========== HELPERS FOR TIFF FILES

        private static short readInt16(BinaryReader r, bool isBigEndian)
        {
            return isBigEndian ? StreamUtils.DecodeBEInt16(r.ReadBytes(2)) : r.ReadInt16();
        }

        private static int readInt32(BinaryReader r, bool isBigEndian)
        {
            return isBigEndian ? StreamUtils.DecodeBEInt32(r.ReadBytes(4)) : r.ReadInt32();
        }

    }
}
