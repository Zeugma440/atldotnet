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


        // ====================================
        // === Font & Color import / export ===
        // ====================================

        public static void SaveFontToStream(Font fnt, BinaryWriter w)
        {
            w.Write(fnt.Name);			// string
            w.Write(fnt.SizeInPoints);	// float = single
            w.Write(fnt.Italic);		// bool
            w.Write(fnt.Bold);			// bool
            w.Write(fnt.Underline);		// bool
            w.Write(fnt.Strikeout);		// bool
        }

        public static void SaveColorToStream(Color col, BinaryWriter w)
        {
            w.Write(col.R);
            w.Write(col.G);
            w.Write(col.B);
        }

        public static Font LoadFontFromStream(BinaryReader r)
        {
            String theName = r.ReadString();
            float theSizePt = r.ReadSingle();
            bool isIta = r.ReadBoolean();
            bool isBold = r.ReadBoolean();
            bool isUnderL = r.ReadBoolean();
            bool isStrike = r.ReadBoolean();

            FontStyle fs = new FontStyle();
            if (isIta) { fs = fs | FontStyle.Italic; }
            if (isBold) { fs = fs | FontStyle.Bold; }
            if (isUnderL) { fs = fs | FontStyle.Underline; }
            if (isStrike) { fs = fs | FontStyle.Strikeout; }

            Font fnt = new Font(theName, theSizePt, fs);
            return fnt;
        }

        public static Color LoadColorFromStream(BinaryReader r)
        {
            return Color.FromArgb(r.ReadByte(), r.ReadByte(), r.ReadByte());
        }

        // ====================================
        // ====================================


        public static String SanitizeFileName(String str)
        {
            return str.Trim().Trim(Path.GetInvalidFileNameChars());
        }

        public static String GetNETRegexpFromDOSRegexp(String dosRegexp)
        {
            return dosRegexp.Replace("*", ".*").Replace("?", ".?");
        }

        public static String StripZeroChars(String iStr)
        {
            return Regex.Replace(iStr, @"\0", "");
        }

        public static String StripEndingZeroChars(String iStr)
        {
            return Regex.Replace(iStr, @"\0+\Z", "");
        }

        public static bool IsOccidentalChar(char c)
        {
            return (
                ((64 < c) && (c < 91)) ||
            ('à' == c) ||
            ('â' == c) ||
            ('ä' == c) ||
            ('Â' == c) ||
            ('Â' == c) ||
            ('À' == c) ||
            ('Ä' == c) ||
            ('ç' == c) ||
            ('Ç' == c) ||
            ('é' == c) ||
            ('è' == c) ||
            ('ë' == c) ||
            ('ê' == c) ||
            ('É' == c) ||
            ('È' == c) ||
            ('Ë' == c) ||
            ('Ê' == c) ||
            ('î' == c) ||
            ('ï' == c) ||
            ('Î' == c) ||
            ('Ï' == c) ||
            ('ñ' == c) ||
            ('Ñ' == c) ||
            ('ô' == c) ||
            ('ö' == c) ||
            ('ò' == c) ||
            ('Ô' == c) ||
            ('Ö' == c) ||
            ('Ò' == c) ||
            ('ù' == c) ||
            ('û' == c) ||
            ('ü' == c) ||
            ('Ù' == c) ||
            ('Û' == c) ||
            ('Ü' == c) ||
            ('ÿ' == c) ||
            ('Ý' == c) ||
            ('Æ' == c) ||
            ('æ' == c)
                );
        }

        /// <summary>
        /// Make the given char displayable in "plain" HTML
        /// </summary>
        /// <param name="c">Char to simplify</param>
        /// <returns>Simplified char</returns>
        public static char HTMLizeChar(char c)
        {
            // http://en.wikipedia.org/wiki/Windows-1252
            if (!IsOccidentalChar(c))
            {
                return c;
            }
            if ('à' == c) return 'a';
            if ('â' == c) return 'a';
            if ('ä' == c) return 'a';
            if ('Â' == c) return 'A';
            if ('Â' == c) return 'A';
            if ('À' == c) return 'A';
            if ('Ä' == c) return 'A';
            if ('ç' == c) return 'c';
            if ('Ç' == c) return 'C';
            if ('é' == c) return 'e';
            if ('è' == c) return 'e';
            if ('ë' == c) return 'e';
            if ('ê' == c) return 'e';
            if ('É' == c) return 'E';
            if ('È' == c) return 'E';
            if ('Ë' == c) return 'E';
            if ('Ê' == c) return 'E';
            if ('î' == c) return 'i';
            if ('ï' == c) return 'i';
            if ('Î' == c) return 'I';
            if ('Ï' == c) return 'I';
            if ('ñ' == c) return 'n';
            if ('Ñ' == c) return 'N';
            if ('ô' == c) return 'o';
            if ('ö' == c) return 'o';
            if ('ò' == c) return 'o';
            if ('Ô' == c) return 'O';
            if ('Ö' == c) return 'O';
            if ('Ò' == c) return 'O';
            if ('ù' == c) return 'u';
            if ('û' == c) return 'u';
            if ('ü' == c) return 'u';
            if ('Ù' == c) return 'U';
            if ('Û' == c) return 'U';
            if ('Ü' == c) return 'U';
            if ('ÿ' == c) return 'y';
            if ('Ý' == c) return 'Y';
            if ('Æ' == c) return 'A';
            if ('æ' == c) return 'a';
            return c;
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

        // Tries to guess public folder path based on "My folder" path
        // Returns null if algorithm fails
        public static String GuessPublicFolderPath(String myFolderPath)
        {
            // Public folder path is obtained by replacing folder name with "Public"
            // in the user's special folder paths
            int accountPosEnd = 0;
            int accountPosStart = myFolderPath.Length;

            while (accountPosStart > -1)
            {
                accountPosEnd = myFolderPath.LastIndexOf(Path.DirectorySeparatorChar, accountPosStart - 1);
                accountPosStart = myFolderPath.LastIndexOf(Path.DirectorySeparatorChar, accountPosEnd - 1);
                myFolderPath = myFolderPath.Substring(0, accountPosStart + 1) + "Public" + myFolderPath.Substring(accountPosEnd, myFolderPath.Length - accountPosEnd);
                if (Directory.Exists(myFolderPath))
                {
                    return myFolderPath;
                }
            }
            return null;
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

        public static Encoding GetLatin1Encoding()
        {
            return latin1Encoding;
        }
    }
}
