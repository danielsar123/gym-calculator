using System.Diagnostics;
using GymCalculator.DataGenerator.Services;
using Microsoft.Extensions.Configuration;

namespace GymCalculator.DataGenerator
{
    class Program
    {
        static void Main(string[] args)
        {
            // Build configuration (JSON first, then CLI overrides)
            var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true)
                .AddCommandLine(args)
                .Build();

            // Get settings from JSON
            var inputFromJson = config["DataGenerator:InputCsvPath"];
            var outputFromJson = config["DataGenerator:OutputDirectory"];

            // CLI overrides (highest priority)
            var inputPath = config["input"] ?? inputFromJson;
            var outputPath = config["output"] ?? outputFromJson;

            if (string.IsNullOrWhiteSpace(inputPath))
            {
                Console.Error.WriteLine("Input CSV path not provided. Use --input or appsettings.json.");
                Environment.Exit(1);
            }

            if (string.IsNullOrWhiteSpace(outputPath))
            {
                Console.Error.WriteLine("Output directory not provided. Use --output or appsettings.json.");
                Environment.Exit(1);
            }

            if (!File.Exists(inputPath))
            {
                Console.Error.WriteLine($"CSV not found at {inputPath}");
                Environment.Exit(1);
            }

            Directory.CreateDirectory(outputPath);

            Console.WriteLine($"Input CSV: {inputPath}");
            Console.WriteLine($"Output Dir: {outputPath}");

            var swTotal = Stopwatch.StartNew();

            var generator = new DataGeneratorService();
            generator.GeneratePerAgeWithPercentiles(inputPath, outputPath);

            Console.WriteLine($"Done in {swTotal.Elapsed.TotalSeconds:F1}s");
        }
    }
}
