using System.Text.Json;
using System.Linq;
using FluentAssertions;
using GymCalculator.DataGenerator.Services;
using Xunit;

namespace GymCalculator.Tests.DataGenerator
{
    public class DataGeneratorServiceTests : IDisposable
    {
        private readonly string _tempDir;
        private readonly string _csvPath;
        private readonly string _outDir;

        public DataGeneratorServiceTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "GymCalcTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _csvPath = Path.Combine(_tempDir, "test.csv");
            _outDir = Path.Combine(_tempDir, "out");
            Directory.CreateDirectory(_outDir);
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, true); } catch { /* ignore */ }
        }

        private void WriteCsv(params string[] lines)
        {
            File.WriteAllText(_csvPath, string.Join(Environment.NewLine, lines));
        }

        [Fact]
        public void Global_PerSex_DotsWilks_DistinctArrays_And_Counts()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "1,24-34,M,93,Yes,raw,100,200,300,15,20",
                "2,24-34,M,93,No,raw,50,150,250,12,18",
                "1,24-34,F,63,Yes,raw,60,120,180,10,16"
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);

            var file2434 = Path.Combine(_outDir, "precomputed_24_34.json");
            File.Exists(file2434).Should().BeTrue();

            var root = JsonDocument.Parse(File.ReadAllText(file2434)).RootElement;
            var global = root.GetProperty("Global");

            var gm = global.GetProperty("M");
            gm.GetProperty("Count").GetInt32().Should().Be(2);
            var mdotsVals = gm.GetProperty("Dots").GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var mdotsPcts = gm.GetProperty("Dots").GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            mdotsVals.Should().Equal(12.0, 15.0);
            // Inclusive ECDF over 2 values -> [50, 100]
            mdotsPcts.Should().Equal(50.0, 100.0);

            var mWilksVals = gm.GetProperty("Wilks").GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var mWilksPcts = gm.GetProperty("Wilks").GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            mWilksVals.Should().Equal(18.0, 20.0);
            mWilksPcts.Should().Equal(50.0, 100.0);

            var gf = global.GetProperty("F");
            gf.GetProperty("Count").GetInt32().Should().Be(1);
            gf.GetProperty("Dots").ValueKind.Should().Be(JsonValueKind.Null);
            gf.GetProperty("Wilks").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void PerWeight_Sex_Tested_Equipment_DistinctCurves_And_PerMetricCounts()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "1,24-34,M,93,Yes,raw,100,200,300,15,20",
                "2,24-34,M,93,Yes,raw,100,210,310,14,19",     // duplicate bench 100
                "3,24-34,M,93,Yes,single-ply,0,250,320,13,18" // 0 bench, positive squat/dl
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);
            var file = Path.Combine(_outDir, "precomputed_24_34.json");
            var root = JsonDocument.Parse(File.ReadAllText(file)).RootElement;

            var wc93M = root.GetProperty("Sex").GetProperty("M")
                            .GetProperty("WeightClasses").GetProperty("93");

            var tested = wc93M.GetProperty("Tested");

            // "all" includes raw + single-ply (3 total)
            tested.GetProperty("Equipment").GetProperty("all").GetProperty("Count").GetInt32().Should().Be(3);

            // raw group (2 lifters)
            var raw = tested.GetProperty("Equipment").GetProperty("raw");
            raw.GetProperty("Count").GetInt32().Should().Be(2);

            // Bench curve for raw: 2 lifters but same value -> distinct arrays collapse to [100], pcts [100], Count=2
            var rawBench = raw.GetProperty("Bench");
            rawBench.Should().NotBeNull();
            rawBench.GetProperty("Count").GetInt32().Should().Be(2);
            rawBench.GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).Should().Equal(100.0);
            rawBench.GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).Should().Equal(100.0);

            // Squat curve for "all": values [200,210,250]; inclusive ECDF over 3 -> [33.33..., 66.66..., 100]
            var allSquat = tested.GetProperty("Equipment").GetProperty("all").GetProperty("Squat");
            allSquat.Should().NotBeNull();
            allSquat.GetProperty("Count").GetInt32().Should().Be(3);
            var svals = allSquat.GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            var spcts = allSquat.GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).ToArray();
            svals.Should().Equal(200.0, 210.0, 250.0);
            spcts.First().Should().BeApproximately(33.33, 0.02);
            spcts[1].Should().BeApproximately(66.67, 0.02);
            spcts.Last().Should().Be(100.0);
        }

        [Fact]
        public void MetricNull_When_LessThanTwoValid_InGroup()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "1,24-34,M,83,Yes,raw,100,0,0,12,18",
                "2,24-34,M,83,Yes,raw,0,0,0,13,19"
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);
            var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outDir, "precomputed_24_34.json"))).RootElement;

            var wc83M = root.GetProperty("Sex").GetProperty("M")
                            .GetProperty("WeightClasses").GetProperty("83");

            var all = wc83M.GetProperty("Tested").GetProperty("Equipment").GetProperty("all");

            // Bench: only one valid lifter -> null
            all.GetProperty("Bench").ValueKind.Should().Be(JsonValueKind.Null);
            // Squat/Deadlift: zero valid -> null
            all.GetProperty("Squat").ValueKind.Should().Be(JsonValueKind.Null);
            all.GetProperty("Deadlift").ValueKind.Should().Be(JsonValueKind.Null);
        }

        [Fact]
        public void Skips_NonNumericPlace_BlankAge_Mx_Sex()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "NS,24-34,M,93,Yes,raw,100,200,300,15,20",   // skip
                "G,24-34,M,93,Yes,raw,120,210,310,16,21",    // skip
                "1,,M,93,Yes,raw,130,220,320,17,22",         // skip (blank AgeClass)
                "1,24-34,Mx,93,Yes,raw,140,230,330,18,23",   // skip (Mx)
                "1,24-34,F,63,Yes,raw,60,120,180,10,16"      // valid
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);

            var file = Path.Combine(_outDir, "precomputed_24_34.json");
            File.Exists(file).Should().BeTrue();

            var root = JsonDocument.Parse(File.ReadAllText(file)).RootElement;

            // Global: only F
            var global = root.GetProperty("Global");
            global.TryGetProperty("M", out _).Should().BeFalse();
            var gf = global.GetProperty("F");
            gf.GetProperty("Count").GetInt32().Should().Be(1);
            gf.GetProperty("Dots").ValueKind.Should().Be(JsonValueKind.Null);

            // Sex tree: only F present; F has only 63 class
            var sexTree = root.GetProperty("Sex");
            sexTree.TryGetProperty("M", out _).Should().BeFalse();

            var fNode = sexTree.GetProperty("F");
            var fWcs = fNode.GetProperty("WeightClasses");
            fWcs.TryGetProperty("93", out _).Should().BeFalse();
            fWcs.GetProperty("63").Should().NotBeNull();
        }

        [Fact]
        public void DistinctArrays_Are_Ascending_And_Pcts_Monotonic()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "1,24-34,M,105,Yes,raw,120,210,300,14,20",
                "2,24-34,M,105,Yes,raw,140,220,310,16,22",
                "3,24-34,M,105,Yes,raw,130,230,320,15,21"
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);
            var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outDir, "precomputed_24_34.json"))).RootElement;

            var grp = root.GetProperty("Sex").GetProperty("M")
                          .GetProperty("WeightClasses").GetProperty("105")
                          .GetProperty("Tested").GetProperty("Equipment").GetProperty("raw");

            foreach (var metric in new[] { "Bench", "Squat", "Deadlift" })
            {
                var curve = grp.GetProperty(metric);
                curve.Should().NotBeNull();

                var vals = curve.GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                var pcts = curve.GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).ToArray();

                vals.Should().BeInAscendingOrder();
                pcts.Should().BeInAscendingOrder();

                // Inclusive ECDF with 3 distinct => [33.33, 66.67, 100]
                pcts.First().Should().BeApproximately(33.33, 0.02);
                pcts.Last().Should().Be(100.0);
            }

            // Global M Dots/Wilks also monotonic
            var gm = root.GetProperty("Global").GetProperty("M");
            foreach (var metric in new[] { "Dots", "Wilks" })
            {
                var curve = gm.GetProperty(metric);
                var vals = curve.GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                var pcts = curve.GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).ToArray();
                vals.Should().BeInAscendingOrder();
                pcts.Should().BeInAscendingOrder();
                pcts.First().Should().BeApproximately(33.33, 0.02);
                pcts.Last().Should().Be(100.0);
            }
        }

        [Fact]
        public void TwoLifters_SameValue_Yields_SingleBreakpoint_With_Count2()
        {
            WriteCsv(
                "Place,AgeClass,Sex,WeightClassKg,Tested,Equipment,Best3BenchKg,Best3SquatKg,Best3DeadliftKg,Dots,Wilks",
                "1,24-34,M,74,Yes,raw,150,0,0,12,18",
                "2,24-34,M,74,Yes,raw,150,0,0,13,19"
            );

            new DataGeneratorService().GeneratePerAgeWithPercentiles(_csvPath, _outDir);
            var root = JsonDocument.Parse(File.ReadAllText(Path.Combine(_outDir, "precomputed_24_34.json"))).RootElement;

            var grp = root.GetProperty("Sex").GetProperty("M")
                          .GetProperty("WeightClasses").GetProperty("74")
                          .GetProperty("Tested").GetProperty("Equipment").GetProperty("raw");

            var bench = grp.GetProperty("Bench");
            bench.GetProperty("Count").GetInt32().Should().Be(2);
            bench.GetProperty("Values").EnumerateArray().Select(e => e.GetDouble()).Should().Equal(150.0);
            bench.GetProperty("Pcts").EnumerateArray().Select(e => e.GetDouble()).Should().Equal(100.0);
        }
    }
}
