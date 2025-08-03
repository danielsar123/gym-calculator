using System.Diagnostics;
using GymCalculator.DataGenerator.Services;
using static GymCalculator.DataGenerator.Constants.Constants;
namespace GymCalculator.DataGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // 1) Resolve CSV path under the project Data folder
            var csvPath = Path.Combine(AppContext.BaseDirectory, DataFilePath);
            var jsonPath = Path.Combine(AppContext.BaseDirectory, OutputFilePath);
            Directory.CreateDirectory(jsonPath);

            if (!File.Exists(csvPath))
            {
                Console.Error.WriteLine($"❌ CSV not found at {csvPath}");
                Environment.Exit(1);
            }

            var swTotal = Stopwatch.StartNew();
            var generator = new DataGeneratorService();
            generator.GeneratePerAgeWithPercentiles(csvPath, jsonPath);
            Console.WriteLine($"Done in {swTotal.Elapsed.TotalSeconds:F1}s — JSON written to {jsonPath}");
        }
    }
}


