using System;
using System.Drawing;
using System.IO;
using System.Text;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;

namespace Commons
{
	/// <summary>
	/// General utility class
	/// </summary>
    public class Utils
    {
        public delegate void voidDelegate();

        public Utils() { }


        public static String ProtectValue(String value)
        {
            return (null == value) ? "" : value;
        }

        public static String FormatTime_ms(long milliseconds)
        {
            long seconds = (long)Math.Floor(milliseconds / 1000.00);

            return FormatTime(seconds) + "." + (milliseconds - seconds * 1000);
        }

        public static String FormatTime(long seconds)
        {
            int h;
            long m;
            String mStr;
            long s;
            String sStr;
            int d;

            h = Convert.ToInt32(Math.Floor(seconds / 3600.00));
            m = Convert.ToInt32(Math.Floor((seconds - 3600.00 * h) / 60));
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

        public static String GetColorCodeFromColor(Color col)
        {
            String res = "#";
            res += NumToHex(Convert.ToInt32(Math.Floor((double)col.R / 16)));
            res += NumToHex(Convert.ToInt32(col.R % 16));

            res += NumToHex(Convert.ToInt32(Math.Floor((double)col.G / 16)));
            res += NumToHex(Convert.ToInt32(col.G % 16));

            res += NumToHex(Convert.ToInt32(Math.Floor((double)col.B / 16)));
            res += NumToHex(Convert.ToInt32(col.B % 16));

            return res;
        }

        // Because nothing simple seems to be available on the .NET framework :/
        private static Char NumToHex(int num)
        {
            if (num < 10) return num.ToString()[0];
            if (10 == num) return 'A';
            if (11 == num) return 'B';
            if (12 == num) return 'C';
            if (13 == num) return 'D';
            if (14 == num) return 'E';
            if (15 == num) return 'F';
            return ' ';
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

        public static String BuildStrictLengthString(String value, int length, char paddingChar)
        {
            String result = (null == value) ? "" : value;

            if (value.Length > length) value = value.Substring(0, length);
            if (value.Length < length) value = value.PadRight(30, paddingChar);

            return result;
        }
    }
}
