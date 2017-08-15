using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Security.Cryptography;

namespace Commons
{
    /// <summary>
    /// General utility class
    /// </summary>
    public class Utils
    {
        /// <summary>
        /// Defines a delegate that does not carry any argument (useful for "pinging")
        /// </summary>
        public delegate void voidDelegate();

        private static Encoding latin1Encoding = Encoding.GetEncoding("ISO-8859-1");

        public static Encoding Latin1Encoding { get { return latin1Encoding; } }


        /// <summary>
        /// Transforms the given string so that is becomes non-null
        /// </summary>
        /// <param name="value">String to protect</param>
        /// <returns>Given string if non-null; else empty string</returns>
        public static String ProtectValue(String value)
        {
            return (null == value) ? "" : value;
        }

        /// <summary>
        /// Format the given duration using the following format
        ///     DDdHH:MM:SS.UUUU
        ///     
        ///  Where
        ///     DD is the number of days, if applicable (i.e. durations of less than 1 day won't display the "DDd" part)
        ///     HH is the number of hours, if applicable (i.e. durations of less than 1 hour won't display the "HH:" part)
        ///     MM is the number of minutes
        ///     SS is the number of seconds
        ///     UUUU is the number of milliseconds
        /// </summary>
        /// <param name="milliseconds">Duration to format (in milliseconds)</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static String FormatTime_ms(long milliseconds)
        {
            long seconds = Convert.ToInt64(Math.Floor(milliseconds / 1000.00));

            return FormatTime(seconds) + "." + (milliseconds - seconds * 1000);
        }

        /// <summary>
        /// Format the given duration using the following format
        ///     DDdHH:MM:SS
        ///     
        ///  Where
        ///     DD is the number of days, if applicable (i.e. durations of less than 1 day won't display the "DDd" part)
        ///     HH is the number of hours, if applicable (i.e. durations of less than 1 hour won't display the "HH:" part)
        ///     MM is the number of minutes
        ///     SS is the number of seconds
        /// </summary>
        /// <param name="seconds">Duration to format (in seconds)</param>
        /// <returns>Formatted duration according to the abovementioned convention</returns>
        public static String FormatTime(long seconds)
        {
            int h;
            long m;
            String hStr, mStr, sStr;
            long s;
            int d;

            h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            m = Convert.ToInt64(Math.Floor((seconds - 3600.00 * h) / 60));
            s = seconds - (60 * m) - (3600 * h);
            d = Convert.ToInt32(Math.Floor(h / 24.00));
            if (d > 0) h = h - (24 * d);

            hStr = h.ToString();
            if (1 == hStr.Length) hStr = "0" + hStr;
            mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

            if (d > 0)
            {
                return d + "d " + hStr + ":" + mStr + ":" + sStr;
            }
            else
            {
                if (h > 0)
                {
                    return hStr + ":" + mStr + ":" + sStr;
                }
                else
                {
                    return mStr + ":" + sStr;
                }
            }
        }

        /// <summary>
        /// Formats a .NET Color to its six-digit "hex triplet" RGB representation (#RRGGBB)
        /// </summary>
        /// <param name="col">Color to be formatted</param>
        /// <returns>Formatted color</returns>
        public static String GetColorCodeFromColor(Color col)
        {
            return "#"+col.ToArgb().ToString("X6").Remove(0,2);
        }

        /// <summary>
        /// Strips the given string from all invalid characters that could prevent it from being a proper file name
        /// </summary>
        /// <param name="str">String to sanitize</param>
        /// <returns>Given string stripped of all characters that are not valid in a file name</returns>
        public static string SanitizeFileName(string str)
        {
            return str.Trim().Trim(Path.GetInvalidFileNameChars());
        }

        /// <summary>
        /// Strips the given string from all null '\0' characters (anywhere in the string)
        /// </summary>
        /// <param name="iStr">String to process</param>
        /// <returns>Given string, without any null character</returns>
        public static string StripZeroChars(string iStr)
        {
            return Regex.Replace(iStr, @"\0", "");
        }

        /// <summary>
        /// Strips the given string from all ending null '\0' characters
        /// </summary>
        /// <param name="iStr">String to process</param>
        /// <returns>Given string, without any ending null character</returns>
        public static string StripEndingZeroChars(string iStr)
        {
            return Regex.Replace(iStr, @"\0+\Z", "");
        }

        /// <summary>
        /// Indicates if the current user has the credentials to read the given file
        /// </summary>
        /// <param name="iFile">Access path to the file to test</param>
        /// <returns>True if the file can be read; false if not</returns>
        public static bool IsFileReadable(string iFile)
        {
            FileIOPermission filePermission = new FileIOPermission(FileIOPermissionAccess.Read, @iFile);
            try
            {
                filePermission.Demand();
                return true;
            }
            catch (SecurityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates if the current user can list the contents of the given folder
        /// e.g. a visible network folder whose access is retricted may be visible, but not "listable"
        /// </summary>
        /// <param name="iFile">Access path to the folder to test</param>
        /// <returns>True if the folder can be listed; false if not</returns>
        public static bool IsFolderListable(string iPath)
        {
            FileIOPermission filePermission = new FileIOPermission(FileIOPermissionAccess.PathDiscovery, @iPath);
            try
            {
                filePermission.Demand();
                return true;
            }
            catch (SecurityException)
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates if the given file can actually be modified.
        /// 
        /// NB : Will be seen as non-modifiable :
        ///   - resources accessible in read-only mode
        ///   - resources accessible in write mode, and locked by another application
        /// </summary>
        /// <param name="iPath">Access path to the folder to test</param>
        /// <returns>True if the file can be modified; false if not</returns>
        public static bool IsFileModifiable(string iPath)
        {
            try
            {
                // NB: The use of fileStream is intentional to test the scenario where file access
                // is granted to the user while the resource is locked by another application
                FileStream fs = new FileStream(iPath, FileMode.Create);
                fs.Close();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        /// <summary>
        /// Indicates if the current environment is running under Mono
        /// </summary>
        /// <returns>True if the current environment is running under Mono; false if not</returns>
        public static bool IsRunningMono()
        {
            Type t = Type.GetType("Mono.Runtime");
            return (t != null);
        }

        /// <summary>
        /// Indicates if the current OS is Windows
        /// </summary>
        /// <returns>True if the current OS is Windows; false if not</returns>
        public static bool IsRunningWindows()
        {
            PlatformID pid = Environment.OSVersion.Platform;
            switch (pid)
            {
                case PlatformID.Win32NT:
                case PlatformID.Win32S:
                case PlatformID.Win32Windows:
                case PlatformID.WinCE:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Indicates if the current OS is Linux/Unix
        /// </summary>
        /// <returns>True if the current OS is Linux/Unix; false if not</returns>
        public static bool IsRunningUnix()
        {
            PlatformID pid = Environment.OSVersion.Platform;
            switch (pid)
            {
                case PlatformID.Unix:
                    return true;
                default:
                    return false;
            }
        }

        /// <summary>
        /// Transforms the given string to format with a given length
        ///  - If the given length is shorter than the actual length of the string, it will be truncated
        ///  - If the given length is longer than the actual length of the string, it will be right/left-padded with a given character
        /// </summary>
        /// <param name="value">String to transform</param>
        /// <param name="length">Target length of the final string</param>
        /// <param name="paddingChar">Character to use if padding is needed</param>
        /// <param name="padRight">True if the padding has to be done on the right-side of the target string; 
        /// false if the padding has to be done on the left-side (optional; default value = true)</param>
        /// <returns>Reprocessed string of given length, according to rules documented in the method description</returns>
        public static string BuildStrictLengthString(string value, int length, char paddingChar, bool padRight = true)
        {
            string result = (null == value) ? "" : value;

            if (result.Length > length) result = result.Substring(0, length);
            else if (result.Length < length)
            {
                if (padRight) result = result.PadRight(length, paddingChar);
                else result = result.PadLeft(length, paddingChar);
            }

            return result;
        }

        /// <summary>
        /// Returns the mime-type of the given .NET image format
        /// NB : This function is restricted to most common embedded picture formats : JPEG, GIF, PNG, BMP
        /// </summary>
        /// <param name="imageFormat">Image format whose mime-type to obtain</param>
        /// <returns>mime-type of the given image format</returns>
        public static string GetMimeTypeFromImageFormat(System.Drawing.Imaging.ImageFormat imageFormat)
        {
            string result = "image/";

            if (imageFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg))
            {
                result += "jpeg";
            }
            else if (imageFormat.Equals(System.Drawing.Imaging.ImageFormat.Gif))
            {
                result += "gif";
            }
            else if (imageFormat.Equals(System.Drawing.Imaging.ImageFormat.Png))
            {
                result += "png";
            }
            else if (imageFormat.Equals(System.Drawing.Imaging.ImageFormat.Bmp))
            {
                result += "bmp";
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

            return result;
        }

        /// <summary>
        /// Resizes the given image to the given dimensions
        /// </summary>
        /// <param name="image">Image to resize</param>
        /// <param name="size">Target dimensions</param>
        /// <param name="preserveAspectRatio">True if the resized image has to keep the same aspect ratio as the given image; false if not (optional; default value = true)</param>
        /// <returns>Resized image</returns>
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
                newWidth = Convert.ToInt32( Math.Round(originalWidth * percent, 0) );
                newHeight = Convert.ToInt32( Math.Round(originalHeight * percent, 0) );
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

        /// <summary>
        /// Get the MD5 hash of the given string, interpreted as UTF-8
        /// Returned value is an Int32 computed from the first 4 bytes of the actual MD5 according to the byte order ("endianness") in which data is stored in the current computer architecture.
        /// 
        /// Warning : Given the nature of MD5 hashes which are coded on 32 bytes, this function actually truncates information !
        /// USE IT AT YOUR OWN RISK
        /// </summary>
        /// <param name="value">String to get the MD5 hash from</param>
        /// <returns>MD5 hash converted to Int32 according to the rules documented in the method description</returns>
        public static int GetInt32MD5Hash(string value)
        {
            return BitConverter.ToInt32(GetMD5Hash(value),0);
        }

        /// <summary>
        /// Get the MD5 hash of the given string, interpreted as UTF-8
        /// Returned value is a string representing the MD5 in hex values
        /// </summary>
        /// <param name="value">Strinsg to get the MD5 hash from</param>
        /// <returns>MD5 hash converted to string according to the rules documented in the method description</returns>
        public static string GetStrMD5Hash(string value)
        {
            StringBuilder sb = new StringBuilder();

            foreach (byte b in GetMD5Hash(value)) { sb.Append(b.ToString("x2")); }

            return sb.ToString();
        }

        /// <summary>
        /// Get the MD5 hash of the given string, interpreted as UTF-8
        /// </summary>
        /// <param name="value">String to get the MD5 hash from</param>
        /// <returns>MD5 hash of the given string</returns>
        private static byte[] GetMD5Hash(string value)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }

        // TODO DOC
        public static bool ToBoolean(string value)
        {
            if (value != null)
            {
                value = value.Trim();

                if (value.Length > 0)
                {
                    // Numeric convert
                    float f;
                    if (float.TryParse(value, out f))
                    {
                        return (f != 0);
                    }
                    else
                    {
                        value = value.ToLower();
                        return ("true".Equals(value));
                    }
                }
            }

            return false;
        }

        /// <summary>
        /// The method to Decode your Base64 strings.
        /// </summary>
        /// <param name="encodedData">The String containing the characters to decode.</param>
        /// <param name="s">The Stream where the resulting decoded data will be written.</param>
        /// Source : http://blogs.microsoft.co.il/blogs/mneiter/archive/2009/03/22/how-to-encoding-and-decoding-base64-strings-in-c.aspx
        public static byte[] DecodeFrom64(byte[] encodedData)
        {
            if (encodedData.Length % 4 > 0) throw new FormatException("Size must me multiple of 4");

            char[] encodedDataChar = new char[encodedData.Length];
            /*
                        for (int i = 0; i < encodedData.Length; i++)
                        {
                            encodedDataChar[i] = System.Convert.ToChar(encodedData[i]);
                        }
            */
            Latin1Encoding.GetChars(encodedData, 0, encodedData.Length, encodedDataChar, 0); // Optimized for large data

            return System.Convert.FromBase64CharArray(encodedDataChar, 0, encodedDataChar.Length);
        }

        // TODO DOC
        public static ImageFormat GetImageFormatFromPictureHeader(byte[] header)
        {
            if (header.Length < 3) throw new FormatException("Header length must be at least 3");

            if (0xFF == header[0] && 0xD8 == header[1] && 0xFF == header[2]) return ImageFormat.Jpeg;
            else if (0x42 == header[0] && 0x4D == header[1]) return ImageFormat.Bmp;
            else if (0x47 == header[0] && 0x49 == header[1] && 0x46 == header[2]) return ImageFormat.Gif;
            else if (0x89 == header[0] && 0x50 == header[1] && 0x4E == header[2]) return ImageFormat.Png;
            else return ImageFormat.Png; // TODO
        }

    }
}
