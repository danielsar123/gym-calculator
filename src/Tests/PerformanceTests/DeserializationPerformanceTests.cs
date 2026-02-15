using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using GymCalculator.DataGenerator.Models;

namespace GymCalculator.Tests.Perf
{
    [Trait("Category", "Performance")]
    public class DeserializationPerfTests
    {
        [Fact]
        public void Precomputed_AgeClass_Files_Deserialize_Within_Performance_Budget()
        {
            var root = Environment.GetEnvironmentVariable("GYMCALC_PRECOMP_DIR");

            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(
                    "GYMCALC_PRECOMP_DIR must be set for Performance tests.");
            }

            Assert.True(Directory.Exists(root),
                $"Precomputed directory not found at: {root}");

            var files = Directory
                .EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly)
                .ToList();

            files.Should().NotBeEmpty(
                "There should be at least one precomputed JSON file in the directory.");

            // Allow CI override of performance threshold
            var maxPerFileMs = int.TryParse(
                Environment.GetEnvironmentVariable("GYMCALC_PERF_MAX_MS_PER_FILE"),
                out var parsed)
                ? parsed
                : 1500;

            // Warm up JIT once
            _ = JsonSerializer.Deserialize<PrecomputedAgeClass>(
                File.ReadAllBytes(files[0]));

            long totalBytes = 0;

            var timings = files.Select(f =>
            {
                var bytes = File.ReadAllBytes(f);
                totalBytes += bytes.LongLength;

                // Median-of-3 measurement to reduce noise
                double[] runs = new double[3];

                for (int i = 0; i < 3; i++)
                {
                    var sw = Stopwatch.StartNew();
                    var model = JsonSerializer.Deserialize<PrecomputedAgeClass>(bytes);
                    sw.Stop();

                    model.Should().NotBeNull(
                        $"File {Path.GetFileName(f)} should deserialize successfully.");

                    runs[i] = sw.Elapsed.TotalMilliseconds;
                }

                Array.Sort(runs);
                var medianMs = runs[1];

                return (File: f, Ms: medianMs, Bytes: bytes.LongLength);
            }).ToList();

            foreach (var t in timings)
            {
                Assert.True(t.Ms <= maxPerFileMs,
                    $"Deserializing {Path.GetFileName(t.File)} took {t.Ms:F1} ms " +
                    $"(limit: {maxPerFileMs} ms).");
            }

            var totalMs = timings.Sum(t => t.Ms);
            var avgMs = timings.Average(t => t.Ms);
            var mb = totalBytes / (1024.0 * 1024.0);
            var sec = totalMs / 1000.0;
            var mbps = sec > 0 ? mb / sec : double.PositiveInfinity;

            Console.WriteLine(
                $"Perf Summary:\n" +
                $"- Files: {files.Count}\n" +
                $"- Total Size: {mb:F1} MB\n" +
                $"- Total Time: {totalMs:F1} ms\n" +
                $"- Avg/File: {avgMs:F1} ms\n" +
                $"- Throughput: ~{mbps:F1} MB/s\n" +
                $"- Threshold/File: {maxPerFileMs} ms\n" +
                $"- Directory: {root}"
            );
        }
    }
}
