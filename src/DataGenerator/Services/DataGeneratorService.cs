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

            long totalRows = 0;
            long skippedRows = 0;

            var numRecordsPerAgeClass = new Dictionary<string, long>(); // AgeClass -> count

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);

            while (csv.Read())
            {
                totalRows++;

                var r = csv.GetRecord<LifterRecord>();

                // skip DQ / NS / DD / G etc (anything non-numeric or <1)
                if (!int.TryParse(r.Place, out var place) || place < 1)
                {
                    skippedRows++;
                    continue;
                }

                if (!double.TryParse(r.Age, NumberStyles.Float, CultureInfo.InvariantCulture, out var ageDouble))
                {
                    skippedRows++;
                    continue;
                }

                var ageInt = (int)Math.Floor(ageDouble);
                var ageClass = AgeClassHelper.ToAgeClass(ageInt);

                var sex = r.Sex?.Trim();
                if (!string.Equals(sex, "M", StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(sex, "F", StringComparison.OrdinalIgnoreCase))
                {
                    skippedRows++;
                    continue;
                }

                // normalize weight class per sex (IPF Open sets)
                var wc = NormalizeWeightClass(r.WeightClassKg, sex!);
                if (wc == null)
                {
                    skippedRows++;
                    continue;
                }

                numRecordsPerAgeClass[ageClass] = numRecordsPerAgeClass.GetValueOrDefault(ageClass) + 1;
                var bySex = buckets.GetOrAdd(ageClass, () => new Dictionary<string, Dictionary<string, Bucket>>());
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

            Console.WriteLine("----- DATA GENERATION SUMMARY -----");
            Console.WriteLine($"Total rows read: {totalRows}");
            Console.WriteLine($"Total rows skipped: {skippedRows}");
            Console.WriteLine($"Total rows used: {totalRows - skippedRows}");
            Console.WriteLine();

            foreach (var kv in numRecordsPerAgeClass.OrderBy(k => k.Key))
            {
                Console.WriteLine($"AgeClass {kv.Key}: {kv.Value} records");
            }

            Console.WriteLine("-----------------------------------");

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
            int before = list.Count;

            int missingAnyLift = list.Count(ld =>
                ld.Best3SquatKg <= 0 ||
                ld.Best3BenchKg <= 0 ||
                ld.Best3DeadliftKg <= 0);     // openpowerlifting csv included Wilks/Dots even if user didn't complete all three lifts, so filter to avoid skewing data

            var valid = list
                .Where(ld => ld.Best3SquatKg > 0 && ld.Best3BenchKg > 0 && ld.Best3DeadliftKg > 0)
                .Select(selector)
                .Where(v => v > 0)
                .ToList();

            int after = valid.Count;

            Console.WriteLine($"[GlobalCurve] Before: {before}, MissingAnyLift: {missingAnyLift}, After: {after}, Removed: {before - after}");

            if (after < 2) return null;

            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(valid);
            return new MetricCurve { Count = after, Values = vals, Pcts = pcts };
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
