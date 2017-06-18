using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace ATL.test
{
    public class TestUtils
    {
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
