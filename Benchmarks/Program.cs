using BenchmarkDotNet.Running;

namespace Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
        }
    }
}
