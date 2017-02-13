using System;
using System.IO;

namespace ATL.AudioData
{
    public class IOBase
    {
        // Optimal settings
        public static int bufferSize = 2048;
        public static FileOptions fileOptions = FileOptions.RandomAccess;

        public static void ChangeFileOptions(FileOptions options)
        {
            fileOptions = options;
        }

        public static void ChangeBufferSize(int bufSize)
        {
            bufferSize = bufSize;
        }

    }
}
