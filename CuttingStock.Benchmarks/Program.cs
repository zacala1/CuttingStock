using BenchmarkDotNet.Running;
using CuttingStock.Benchmarks;

namespace CuttingStock.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  CuttingStock 알고리즘 성능 벤치마크");
            Console.WriteLine("=================================================");
            Console.WriteLine();
            Console.WriteLine("벤치마크를 시작합니다...");
            Console.WriteLine();

            // BenchmarkDotNet 실행
            var summary1D = BenchmarkRunner.Run<AlgorithmBenchmarks>();
            Console.WriteLine($"1D 결과: {summary1D.ResultsDirectoryPath}");
            Console.WriteLine();

            var summary2D = BenchmarkRunner.Run<TwoDBenchmarks>();
            Console.WriteLine($"2D 결과: {summary2D.ResultsDirectoryPath}");
            Console.WriteLine();
            Console.WriteLine("벤치마크가 완료되었습니다.");
        }
    }
}
