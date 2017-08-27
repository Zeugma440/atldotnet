using BenchmarkDotNet.Running;
using System;

namespace ATL.benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            //BenchmarkRunner.Run<Speed>();

            //BenchmarkRunner.Run<Memory>();

            readAt(TestUtils.GetResourceLocationRoot() + "OGG/singlePicture.ogg", true);
        }

        static private void readAt(string filePath, bool useOldImplementation = false)
        {
            Console.WriteLine(filePath);

            Track t = new Track(filePath, useOldImplementation);
            t.GetEmbeddedPicture(useOldImplementation);

            Console.WriteLine(t.Title);

            Console.WriteLine("end");
            Console.ReadLine();
        }
    }
}
