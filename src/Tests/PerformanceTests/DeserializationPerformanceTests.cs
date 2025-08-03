using System.Diagnostics;
using System.Text.Json;
using FluentAssertions;
using GymCalculator.DataGenerator.Models;

namespace GymCalculator.Tests.Perf
{
    public class DeserializationPerfTests
    {
        // Default directory (same as schema validator)
        private const string DefaultDir =
            @"C:\src\Repos\GymCalculator\src\DataGenerator\bin\Debug\net9.0\Data\PWLiftingDataSortedByAgeClass";

        // Optionally tighten/loosen per-file threshold:
        //   set GYMCALC_PERF_MAX_MS_PER_FILE=1200
        // Optionally override directory:
        //   set GYMCALC_PRECOMP_DIR=C:\...\PWLiftingDataSortedByAgeClass
        [Fact]
        public void Precomputed_AgeClass_Files_Deserialize_Quickly()
        {
            var root = ResolvePrecomputedDir();
            Assert.True(Directory.Exists(root),
                $"Precomputed directory not found. Looked for: {root}\n" +
                "Set env var GYMCALC_PRECOMP_DIR to override this path.");

            var files = Directory.EnumerateFiles(root, "*.json", SearchOption.TopDirectoryOnly).ToList();
            files.Count.Should().BeGreaterThan(0, "there should be at least one precomputed json file in the directory");

            // Warm-up JIT
            _ = JsonSerializer.Deserialize<PrecomputedAgeClass>(File.ReadAllBytes(files[0]));

            long totalBytes = 0;
            var timings = files.Select(f =>
            {
                var bytes = File.ReadAllBytes(f);
                totalBytes += bytes.LongLength;

                var sw = Stopwatch.StartNew();
                var model = JsonSerializer.Deserialize<PrecomputedAgeClass>(bytes);
                sw.Stop();

                model.Should().NotBeNull($"file {Path.GetFileName(f)} should deserialize");
                model!.Global.Should().NotBeNull();

                return (File: f, Ms: sw.Elapsed.TotalMilliseconds, Bytes: (long)bytes.LongLength);
            }).ToList();

            var totalMs = timings.Sum(t => t.Ms);
            var avgMs = timings.Average(t => t.Ms);
            var mb = totalBytes / (1024.0 * 1024.0);
            var sec = totalMs / 1000.0;
            var mbps = sec > 0 ? mb / sec : double.PositiveInfinity;

            var maxPerFileMs = GetEnvInt("GYMCALC_PERF_MAX_MS_PER_FILE", 1500);
            foreach (var t in timings)
            {
                Assert.True(t.Ms <= maxPerFileMs,
                    $"Deserializing {Path.GetFileName(t.File)} took {t.Ms:F1} ms (limit {maxPerFileMs} ms).");
            }

            Console.WriteLine($"Perf: {files.Count} files, {mb:F1} MB total, total {totalMs:F1} ms, " +
                              $"avg {avgMs:F1} ms/file, throughput ~{mbps:F1} MB/s\n" +
                              $"Dir: {root}");
        }

        private static int GetEnvInt(string name, int fallback)
        {
            var v = Environment.GetEnvironmentVariable(name);
            return int.TryParse(v, out var n) ? n : fallback;
        }

        private static string ResolvePrecomputedDir()
        {
            var env = Environment.GetEnvironmentVariable("GYMCALC_PRECOMP_DIR");
            return !string.IsNullOrWhiteSpace(env) ? env! : DefaultDir;
        }
    }
}
