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

            //string filePath = TestUtils.GetResourceLocationRoot() + "/OGG/ogg_bigPicture.ogg";
            string filePath = @"E:\Dev\Source\Repos\atldotnet\ATL.benchmark\Resources/OGG/ogg_bigPicture.ogg";

            Console.WriteLine(Environment.CurrentDirectory);

            Console.ReadLine();

            Track t = new Track(filePath);
            t.GetEmbeddedPicture();

            Console.ReadLine();
        }
    }
}
