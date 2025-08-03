// Services/DataGeneratorService.cs
using CsvHelper;
using GymCalculator.DataGenerator.Models;
using GymCalculator.DataGenerator.Utilities;
using GymCalculator.Utilities;
using System.Globalization;
using System.Text.Json;

namespace GymCalculator.DataGenerator.Services
{
    public class DataGeneratorService
    {
        public void GeneratePerAgeWithPercentiles(string csvPath, string outDir)
        {
            // AgeClass -> Sex -> NormWeightClass -> Bucket
            var buckets = new Dictionary<string, Dictionary<string, Dictionary<string, Bucket>>>();

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            while (csv.Read())
            {
                var r = csv.GetRecord<LifterRecord>();

                // skip DQ / NS / DD / G etc (anything non-numeric or <1)
                if (!int.TryParse(r.Place, out var place) || place < 1) continue;

                if (string.IsNullOrWhiteSpace(r.AgeClass)) continue;

                var sex = r.Sex?.Trim();
                if (!string.Equals(sex, "M", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase))
                    continue;

                // normalize weight class per sex (IPF Open sets)
                var wc = NormalizeWeightClass(r.WeightClassKg, sex!);
                if (wc == null) continue;

                var bySex = buckets.GetOrAdd(r.AgeClass, () => new Dictionary<string, Dictionary<string, Bucket>>());
                var byWeight = bySex.GetOrAdd(sex!, () => new Dictionary<string, Bucket>());
                var bucket = byWeight.GetOrAdd(wc, () => new Bucket());

                var data = new LifterData(r);
                if (r.TestedEqualsYes()) bucket.Tested.Add(data);
                else bucket.Untested.Add(data);
            }

            foreach (var ageKv in buckets)
            {
                var ageClass = ageKv.Key;
                var safeName = ageClass.Replace('-', '_');
                var outputFile = Path.Combine(outDir, $"precomputed_{safeName}.json");

                // Global (per sex) Dots/Wilks distinct curves
                var globalBySex = new Dictionary<string, GlobalMetrics>();
                foreach (var sexKv in ageKv.Value)
                {
                    var lifters = sexKv.Value.Values
                        .SelectMany(b => b.Tested.Concat(b.Untested))
                        .ToList();

                    globalBySex[sexKv.Key] = new GlobalMetrics
                    {
                        Count = lifters.Count,
                        Dots = BuildGlobalCurve(lifters, ld => ld.Dots),
                        Wilks = BuildGlobalCurve(lifters, ld => ld.Wilks)
                    };
                }

                // Build Sex -> WeightClasses tree
                var sexTree = new Dictionary<string, SexNode>();
                foreach (var sexKv in ageKv.Value)
                {
                    var sexNode = new SexNode { WeightClasses = new Dictionary<string, WeightClassMetrics>() };

                    foreach (var wcKv in sexKv.Value)
                    {
                        var bucket = wcKv.Value;

                        var wcModel = new WeightClassMetrics
                        {
                            Tested = BuildTestGroupDistinct(bucket.Tested),
                            Untested = BuildTestGroupDistinct(bucket.Untested)
                        };

                        sexNode.WeightClasses[wcKv.Key] = wcModel;
                    }

                    sexTree[sexKv.Key] = sexNode;
                }

                var model = new PrecomputedAgeClass
                {
                    Global = globalBySex,
                    Sex = sexTree
                };

                Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
                using var fs = File.Create(outputFile);
                using var writer = new Utf8JsonWriter(fs, new JsonWriterOptions { Indented = true });
                JsonSerializer.Serialize(writer, model);
                Console.WriteLine($"Wrote {Path.GetFileName(outputFile)}");
            }
        }

        private TestGroupMetrics BuildTestGroupDistinct(List<LifterData> lifters)
        {
            var tg = new TestGroupMetrics { Equipment = new Dictionary<string, EquipmentMetrics>() };

            foreach (var (eq, list) in new[] { ("all", lifters) }
                     .Concat(lifters.GroupBy(ld => ld.Equipment).Select(g => (g.Key, g.ToList()))))
            {
                int benchCount = list.Count(ld => ld.Best3BenchKg > 0);
                int squatCount = list.Count(ld => ld.Best3SquatKg > 0);
                int deadliftCount = list.Count(ld => ld.Best3DeadliftKg > 0);

                var em = new EquipmentMetrics
                {
                    Count = list.Count, // group size (equip+tested)
                    Bench = benchCount < 2 ? null : BuildDistinctCurve(list.Select(ld => ld.Best3BenchKg), benchCount),
                    Squat = squatCount < 2 ? null : BuildDistinctCurve(list.Select(ld => ld.Best3SquatKg), squatCount),
                    Deadlift = deadliftCount < 2 ? null : BuildDistinctCurve(list.Select(ld => ld.Best3DeadliftKg), deadliftCount)
                };

                tg.Equipment[eq] = em;
            }

            return tg;
        }

        private MetricCurve? BuildGlobalCurve(List<LifterData> list, Func<LifterData, double> selector)
        {
            var valid = list.Select(selector).Where(v => v > 0).ToList();
            if (valid.Count < 2) return null;

            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(valid);
            return new MetricCurve
            {
                Count = valid.Count,  // lifters contributing to this metric
                Values = vals,         // distinct breakpoints
                Pcts = pcts
            };
        }

        private static MetricCurve BuildDistinctCurve(IEnumerable<double> values, int countForMetric)
        {
            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(values);
            return new MetricCurve
            {
                Count = countForMetric, // lifters with >0 for THIS metric
                Values = vals,
                Pcts = pcts
            };
        }

        // --- Normalization (IPF Open classes) ---
        // Men: 59,66,74,83,93,105,120,120+
        // Women: 47,52,57,63,69,76,84,84+
        private static string? NormalizeWeightClass(string raw, string sex)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;

            // parse values like "93" or "93+" or "93.5"
            if (!double.TryParse(raw.TrimEnd('+'),
                                  NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
                                  CultureInfo.InvariantCulture,
                                  out var w))
                return null;

            double[] thresholds = string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase)
                ? new[] { 47d, 52, 57, 63, 69, 76, 84 }
                : new[] { 59d, 66, 74, 83, 93, 105, 120 };

            foreach (var t in thresholds)
            {
                if (w <= t) return $"{t}";
            }
            // above highest threshold
            var max = thresholds.Last();
            return $"{max}+";
        }
    }
}
