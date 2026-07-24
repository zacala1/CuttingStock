using BenchmarkDotNet.Running;

namespace CuttingStock.Benchmarks
{
    internal static class Program
    {
        private const string DefaultMode = "--default";

        private static void Main(string[] args)
        {
            Console.WriteLine("=================================================");
            Console.WriteLine("  CuttingStock 알고리즘 성능 벤치마크");
            Console.WriteLine("=================================================");
            Console.WriteLine();

            var mode = args.FirstOrDefault()?.ToLowerInvariant() ?? DefaultMode;
            switch (mode)
            {
                case DefaultMode:
                    Run<AlgorithmBenchmarks>("1D");
                    Run<TwoDBenchmarks>("2D");
                    break;

                case "--quality":
                    Run<QualityBenchmarks>("품질");
                    break;

                case "--large":
                    Run<LargeScaleBenchmarks>("1,000건 대규모");
                    break;

                case "--all":
                    Run<AlgorithmBenchmarks>("1D");
                    Run<TwoDBenchmarks>("2D");
                    Run<QualityBenchmarks>("품질");
                    Run<LargeScaleBenchmarks>("1,000건 대규모");
                    break;

                case "--help":
                case "-h":
                    PrintUsage();
                    return;

                default:
                    Console.Error.WriteLine($"알 수 없는 벤치마크 모드: {mode}");
                    PrintUsage();
                    Environment.ExitCode = 2;
                    return;
            }

            Console.WriteLine("벤치마크가 완료되었습니다.");
        }

        private static void Run<TBenchmark>(string label)
        {
            Console.WriteLine($"{label} 벤치마크를 시작합니다...");
            var summary = BenchmarkRunner.Run<TBenchmark>();
            Console.WriteLine($"{label} 결과: {summary.ResultsDirectoryPath}");
            Console.WriteLine();
        }

        private static void PrintUsage()
        {
            Console.WriteLine("사용법: dotnet run -c Release -- [--default|--quality|--large|--all]");
            Console.WriteLine("  --default  1D 및 2D 속도/메모리 벤치마크 (기본값)");
            Console.WriteLine("  --quality  솔버 품질 비교");
            Console.WriteLine("  --large    Greedy 1,000건 장시간 벤치마크");
            Console.WriteLine("  --all      모든 벤치마크");
        }
    }
}
