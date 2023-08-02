using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ATL.UI_test
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

                locationRoot += Path.DirectorySeparatorChar + "ATL.unit-test" + Path.DirectorySeparatorChar + "Resources";
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

        public static async Task<string> GenerateTempTestFileAsync(string fileName, int index = -1)
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
            await CopyFileAsync(originPath, result);
            FileInfo fileInfo = new FileInfo(result);
            fileInfo.IsReadOnly = false;

            return result;
        }

        public static async Task CopyFileAsync(string sourceFile, string destinationFile)
        {
            using (var sourceStream = new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, 8 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
            using (var destinationStream = new FileStream(destinationFile, FileMode.CreateNew, FileAccess.Write, FileShare.None, 8 * 1024, FileOptions.Asynchronous | FileOptions.SequentialScan))
                await sourceStream.CopyToAsync(destinationStream);
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
