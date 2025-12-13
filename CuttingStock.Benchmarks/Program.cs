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
            var summary = BenchmarkRunner.Run<AlgorithmBenchmarks>();

            Console.WriteLine();
            Console.WriteLine("벤치마크가 완료되었습니다.");
            Console.WriteLine($"결과는 {summary.ResultsDirectoryPath} 폴더에 저장되었습니다.");
        }
    }
}
