using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ATL.test
{
    public class TestUtils
    {
        public static string REPO_NAME = "atldotnet";
        private static string locationRoot = null;

        public static string GetResourceLocationRoot(bool includeFinalSeparator = true)
        {
            if (null == locationRoot)
            {
                locationRoot = Environment.CurrentDirectory;

                locationRoot = locationRoot.Substring(0, locationRoot.IndexOf(REPO_NAME) + REPO_NAME.Length);

                locationRoot += Path.DirectorySeparatorChar + "ATL.test" + Path.DirectorySeparatorChar + "Resources";
            }

            if (includeFinalSeparator) return locationRoot + Path.DirectorySeparatorChar; else return locationRoot;
        }

        public static string CopyAsTempTestFile(string fileName)
        {
            string extension = fileName.Substring(fileName.LastIndexOf('.'), fileName.Length - fileName.LastIndexOf('.'));
            int lastSeparatorPos = Math.Max(fileName.LastIndexOf('\\'), fileName.LastIndexOf('/'));
            string bareFileName = fileName.Substring(lastSeparatorPos+1, fileName.Length - lastSeparatorPos - 1);
            if (!Directory.Exists(GetResourceLocationRoot()+"tmp")) Directory.CreateDirectory(GetResourceLocationRoot() + "tmp");
            string result = GetResourceLocationRoot() + "tmp" + Path.DirectorySeparatorChar + bareFileName + "--" + DateTime.Now.ToString("HH.mm.ss.fff") + extension;

            // Create a writable working copy
            File.Copy(GetResourceLocationRoot() + fileName, result, true);
            FileInfo fileInfo = new FileInfo(result);
            fileInfo.IsReadOnly = false;

            return result;
        }

        public static string CreateTempTestFile(string fileName)
        {
            string extension = fileName.Substring(fileName.LastIndexOf('.'), fileName.Length - fileName.LastIndexOf('.'));
            int lastSeparatorPos = Math.Max(fileName.LastIndexOf('\\'), fileName.LastIndexOf('/'));
            string bareFileName = fileName.Substring(lastSeparatorPos + 1, fileName.Length - lastSeparatorPos - 1);
            if (!Directory.Exists(GetResourceLocationRoot() + "tmp")) Directory.CreateDirectory(GetResourceLocationRoot() + "tmp");
            string result = GetResourceLocationRoot() + "tmp" + Path.DirectorySeparatorChar + bareFileName + "--" + DateTime.Now.ToString("HH.mm.ss.fff") + extension;

            // Create a writable file from scratch
            Stream s = File.Create(result);
            s.Close();

            FileInfo fileInfo = new FileInfo(result);
            fileInfo.IsReadOnly = false;

            return result;
        }

        public static string CopyFileAndReplace(string location, string placeholder, string replacement)
        {
            IList<KeyValuePair<string, string>> replacements = new List<KeyValuePair<string, string>>();
            replacements.Add(new KeyValuePair<string, string>(placeholder, replacement));
            return CopyFileAndReplace(location, replacements);
        }

        public static string CopyFileAndReplace(string location, IList<KeyValuePair<string, string>> replacements)
        {
            string testFileLocation = location.Substring(0, location.LastIndexOf('.')) + "_test" + location.Substring(location.LastIndexOf('.'), location.Length - location.LastIndexOf('.'));
            string replacedLine;

            using (StreamWriter s = File.CreateText(testFileLocation))
            {
                foreach (string line in File.ReadLines(location))
                {
                    replacedLine = line;
                    foreach (KeyValuePair<string, string> kvp in replacements) replacedLine = replacedLine.Replace(kvp.Key, kvp.Value);
                    s.WriteLine(replacedLine);
                }
            }

            return testFileLocation;
        }

        public static string GetFileMD5Hash(string filename)
        {
            using (MD5 md5 = MD5.Create())
            {
                using (FileStream stream = File.OpenRead(filename))
                {
                    return Encoding.Default.GetString(md5.ComputeHash(stream));
                }
            }
        }

        public static string GetStreamMD5Hash(Stream stream)
        {
            using (MD5 md5 = MD5.Create())
            {
                return Encoding.Default.GetString(md5.ComputeHash(stream));
            }
        }

    }

}
