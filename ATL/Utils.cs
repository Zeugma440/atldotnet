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
            String mStr;
            long s;
            String sStr;
            int d;

            h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            m = Convert.ToInt64(Math.Floor((seconds - 3600.00 * h) / 60));
            s = seconds - (60 * m) - (3600 * h);
            d = Convert.ToInt32(Math.Floor(h / 24.00));
            if (d > 0) h = h - (24 * d);

            mStr = m.ToString();
            if (1 == mStr.Length) mStr = "0" + mStr;
            sStr = s.ToString();
            if (1 == sStr.Length) sStr = "0" + sStr;

            if (d > 0)
            {
                return d + "d " + h + ":" + mStr + ":" + sStr;
            }
            else
            {
                if (h > 0)
                {
                    return h + ":" + mStr + ":" + sStr;
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

        public static String SanitizeFileName(String str)
        {
            return str.Trim().Trim(Path.GetInvalidFileNameChars());
        }

        public static String StripZeroChars(String iStr)
        {
            return Regex.Replace(iStr, @"\0", "");
        }

        public static String StripEndingZeroChars(String iStr)
        {
            return Regex.Replace(iStr, @"\0+\Z", "");
        }

        public static bool IsFileReadable(String iFile)
        {
            FileIOPermission filePermission = new
                FileIOPermission(FileIOPermissionAccess.Read,
                @iFile);
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

        // WARNING : Doesn't work yet...
        public static bool IsFolderListable(String iPath)
        {
            FileIOPermission filePermission = new
                    FileIOPermission(FileIOPermissionAccess.PathDiscovery,
                                     @iPath);
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

        // Check if the given file can be accessed by Creation mode
        // NB : The use of fileStream is intentional to test the scenario where file access is
        // granted to the user while the resource is locked by another application
        public static bool IsFileModifiable(String iPath)
        {
            try
            {
                FileStream fs = new FileStream(iPath, FileMode.Create);
                fs.Close();
                return true;
            }
            catch (IOException)
            {
                return false;
            }
        }

        // Returns true if the app is running under Mono
        public static bool IsRunningMono()
        {
            Type t = Type.GetType("Mono.Runtime");
            return (t != null);
        }

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

        public static String BuildStrictLengthString(String value, int length, char paddingChar, bool padRight = true)
        {
            String result = (null == value) ? "" : value;

            if (result.Length > length) result = result.Substring(0, length);
            if (result.Length < length)
            {
                if (padRight) result = result.PadRight(length, paddingChar);
                else result = result.PadLeft(length, paddingChar);
            }

            return result;
        }

        public static String GetMimeTypeFromImageFormat(System.Drawing.Imaging.ImageFormat imageFormat)
        {
            String result = "image/";

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

        public static Image ResizeImage(Image image, Size size, bool preserveAspectRatio = true)
        {
            int newWidth;
            int newHeight;
            if (preserveAspectRatio)
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;
                float percentWidth = (float)size.Width / (float)originalWidth;
                float percentHeight = (float)size.Height / (float)originalHeight;
                float percent = percentHeight < percentWidth ? percentHeight : percentWidth;
                newWidth = (int)(originalWidth * percent);
                newHeight = (int)(originalHeight * percent);
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

        public static int GetInt32MD5Hash(string value)
        {
            return BitConverter.ToInt32(GetMD5Hash(value),0);
        }

        public static string GetStrMD5Hash(string value)
        {
            return Encoding.Default.GetString(GetMD5Hash(value));
        }

        private static byte[] GetMD5Hash(string value)
        {
            using (var md5 = MD5.Create())
            {
                return md5.ComputeHash(Encoding.UTF8.GetBytes(value));
            }
        }
    }
}
