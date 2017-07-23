using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ATL.test
{
    public class TestUtils
    {
        public static string REPO_NAME = "atldotnet";
        private static string locationRoot = null;

        public static string GetResourceLocationRoot()
        {
            if (null == locationRoot)
            {
                locationRoot = Environment.CurrentDirectory;

                locationRoot = locationRoot.Substring(0, locationRoot.IndexOf(REPO_NAME) + REPO_NAME.Length);

                locationRoot += Path.DirectorySeparatorChar + "ATL.test" + Path.DirectorySeparatorChar + "Resources" + Path.DirectorySeparatorChar;
            }

            return locationRoot;
        }

        public static string GetTempTestFile(string fileName)
        {
            string extension = fileName.Substring(fileName.LastIndexOf('.'), fileName.Length - fileName.LastIndexOf('.'));
            if (!Directory.Exists(GetResourceLocationRoot()+"tmp")) Directory.CreateDirectory(GetResourceLocationRoot() + "tmp");
            string result = GetResourceLocationRoot() + "tmp" + Path.DirectorySeparatorChar + fileName + "--" + DateTime.Now.ToLongTimeString().Replace(":", ".") + extension;

            // Create writable a working copy
            File.Copy(GetResourceLocationRoot() + fileName, result, true);
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
