using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ATL.benchmark
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

        public static string GenerateTempTestFile(string fileName, int index = -1)
        {
            string extension = fileName.Substring(fileName.LastIndexOf('.'), fileName.Length - fileName.LastIndexOf('.'));
            int lastSeparatorPos = Math.Max(fileName.LastIndexOf('\\'), fileName.LastIndexOf('/'));
            string bareFileName = fileName.Substring(lastSeparatorPos + 1, fileName.Length - lastSeparatorPos - 1);
            string specifics = (index > -1) ? index.ToString() : "--" + DateTime.Now.ToLongTimeString().Replace(":", ".");
            if (!Directory.Exists(GetResourceLocationRoot() + "tmp")) Directory.CreateDirectory(GetResourceLocationRoot() + "tmp");
            string result = GetResourceLocationRoot() + "tmp" + Path.DirectorySeparatorChar + bareFileName + specifics + extension;

            // Create writable a working copy
            string originPath = fileName;
            if (!Path.IsPathRooted(originPath)) originPath = GetResourceLocationRoot() + originPath;
            File.Copy(originPath, result, true);
            FileInfo fileInfo = new FileInfo(result);
            fileInfo.IsReadOnly = false;

            return result;
        }

        public static string GetFileMD5Hash(string filename)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(filename))
                {
                    return Encoding.Default.GetString(md5.ComputeHash(stream));
                }
            }
        }

    }

}
