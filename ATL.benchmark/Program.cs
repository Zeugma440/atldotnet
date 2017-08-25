using BenchmarkDotNet.Running;

namespace ATL.benchmark
{
    class Program
    {
        static void Main(string[] args)
        {
            BenchmarkRunner.Run<Speed>();
        }
    }
}
