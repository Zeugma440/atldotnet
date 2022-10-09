using System.IO;
using BenchmarkDotNet.Attributes;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using ATL.AudioData;
using ATL.AudioData.IO;

namespace ATL.benchmark
{
    public class Reduce
    {
        public void reduce(string filePath)
        {
            string testFileLocation = TestUtils.GenerateTempTestFile(filePath);

            Track track = new Track(testFileLocation);
            track.Remove(MetaDataIOFactory.TagType.NATIVE);
        }
    }
}
