using FluentAssertions;
using GymCalculator.DataGenerator.Models;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GymCalculator.Tests.ArtifactValidation
{
    [Trait("Category", "Artifact")]
    public class PrecomputedSchemaArtifactTests
    {
        // Allowed IPF Open classes per sex:
        private static readonly HashSet<string> MaleWc = new(StringComparer.Ordinal)
            { "59","66","74","83","93","105","120","120+" };
        private static readonly HashSet<string> FemaleWc = new(StringComparer.Ordinal)
            { "47","52","57","63","69","76","84","84+" };

        [Fact]
        public void All_Files_Conform_To_Schema_And_Invariants()
        {
            var root = Environment.GetEnvironmentVariable("GYMCALC_PRECOMP_DIR");

            if (string.IsNullOrWhiteSpace(root))
            {
                throw new InvalidOperationException(
                    "GYMCALC_PRECOMP_DIR must be set for Artifact tests.");
            }

            Directory.Exists(root).Should().BeTrue($"Precomputed dir not found: {root}");

            var files = Directory.GetFiles(root, "*.json");
            files.Length.Should().BeGreaterThan(0, "no precomputed files found");

            int totalFiles = 0, totalCurves = 0, totalIssues = 0;

            foreach (var file in files)
            {
                totalFiles++;
                var json = File.ReadAllText(file);
                var model = JsonSerializer.Deserialize<PrecomputedAgeClass>(json);
                model.Should().NotBeNull($"Failed to deserialize {Path.GetFileName(file)}");
                if (model == null) continue;

                // 1) Global per sex
                model.Global.Should().NotBeNull("Global block is required");
                foreach (var sexEntry in model.Global)
                {
                    sexEntry.Key.Should().MatchRegex("^(M|F)$", "Global keys should be 'M' or 'F'");
                    var gm = sexEntry.Value;
                    gm.Should().NotBeNull();

                    if (gm.Dots is not null)
                        totalIssues += ValidateCurve("Global->Dots", gm.Dots);
                    if (gm.Wilks is not null)
                        totalIssues += ValidateCurve("Global->Wilks", gm.Wilks);

                    // If a curve exists it must have >=2 contributors
                    if (gm.Dots is not null) gm.Dots.Count.Should().BeGreaterThanOrEqualTo(2);
                    if (gm.Wilks is not null) gm.Wilks.Count.Should().BeGreaterThanOrEqualTo(2);
                }

                // 2) Sex -> WeightClasses
                model.Sex.Should().NotBeNull("Sex tree is required");
                foreach (var sexNodeKv in model.Sex)
                {
                    var sex = sexNodeKv.Key;
                    sex.Should().MatchRegex("^(M|F)$", "Sex keys must be 'M' or 'F'");

                    var wcMap = sexNodeKv.Value.WeightClasses;
                    wcMap.Should().NotBeNull();

                    foreach (var wcKv in wcMap)
                    {
                        var wc = wcKv.Key;
                        if (sex == "M") MaleWc.Contains(wc).Should().BeTrue($"Bad male weight class '{wc}'");
                        if (sex == "F") FemaleWc.Contains(wc).Should().BeTrue($"Bad female weight class '{wc}'");

                        var wcm = wcKv.Value;
                        wcm.Should().NotBeNull();

                        foreach (var (label, tg) in new[] { ("Tested", wcm.Tested), ("Untested", wcm.Untested) })
                        {
                            tg.Should().NotBeNull($"{sex}/{wc}/{label} missing");
                            var eqMap = tg!.Equipment;
                            eqMap.Should().NotBeNull();
                            eqMap.Count.Should().BeGreaterThan(0);

                            // "all" should exist
                            eqMap.ContainsKey("all").Should().BeTrue($"{sex}/{wc}/{label} missing 'all' equipment");

                            foreach (var eqKv in eqMap)
                            {
                                var eq = eqKv.Key; // "all", "raw", "single-ply", etc.
                                var em = eqKv.Value;

                                em.Count.Should().BeGreaterThanOrEqualTo(0);

                                // Validate each optional curve
                                if (em.Bench is not null) totalIssues += ValidateCurve($"{sex}/{wc}/{label}/{eq}/Bench", em.Bench);
                                if (em.Squat is not null) totalIssues += ValidateCurve($"{sex}/{wc}/{label}/{eq}/Squat", em.Squat);
                                if (em.Deadlift is not null) totalIssues += ValidateCurve($"{sex}/{wc}/{label}/{eq}/Deadlift", em.Deadlift);

                                totalCurves += new[] { em.Bench, em.Squat, em.Deadlift }.Count(c => c is not null);
                            }
                        }
                    }
                }
            }

            Console.WriteLine($"Validated {totalFiles} files, {totalCurves} curves, issues found: {totalIssues}");
            totalIssues.Should().Be(0, "All curves should pass structural/monotonic checks");
        }

        private static int ValidateCurve(string label, MetricCurve c)
        {
            int issues = 0;

            // Count & arrays
            if (c.Values is null || c.Pcts is null)
            {
                Console.WriteLine($"[ERR] {label}: Values/Pcts null");
                return 1;
            }
            if (c.Values.Length != c.Pcts.Length)
            {
                Console.WriteLine($"[ERR] {label}: Values.Length != Pcts.Length ({c.Values.Length} vs {c.Pcts.Length})");
                issues++;
            }

            // Distinct → strictly ascending Values
            if (!IsStrictlyAscending(c.Values))
            {
                Console.WriteLine($"[ERR] {label}: Values not strictly ascending");
                issues++;
            }

            // Pcts non-decreasing, in [0,100]
            if (!IsNonDecreasing(c.Pcts))
            {
                Console.WriteLine($"[ERR] {label}: Pcts not non-decreasing");
                issues++;
            }
            if (c.Pcts.Any(p => p < 0 || p > 100))
            {
                Console.WriteLine($"[ERR] {label}: Pcts out of [0,100]");
                issues++;
            }

            // Endpoints: with inclusive ECDF and >=2 distinct points,
            // the first percentile is > 0 (Count(min)/Total * 100) and < 100.
            // With a single distinct point, it's 100.
            if (c.Pcts.Length >= 2)
            {
                // First percentile must be strictly between 0 and 100
                if (!(c.Pcts.First() > 0.0 && c.Pcts.First() < 100.0))
                {
                    Console.WriteLine($"[ERR] {label}: First percentile should be > 0 and < 100 for >=2 points (inclusive ECDF)");
                    issues++;
                }
                // Last percentile should be 100
                if (Math.Abs(c.Pcts.Last() - 100.0) > 1e-9)
                {
                    Console.WriteLine($"[ERR] {label}: Last percentile should be 100 for >=2 points");
                    issues++;
                }
            }
            else if (c.Pcts.Length == 1)
            {
                if (Math.Abs(c.Pcts[0] - 100.0) > 1e-9)
                {
                    Console.WriteLine($"[ERR] {label}: Single-point curve should be 100% (inclusive ECDF)");
                    issues++;
                }
            }

            // Count sanity: distinct arrays imply Count >= Values.Length (due to grouped duplicates)
            if (c.Count < c.Values.Length)
            {
                Console.WriteLine($"[ERR] {label}: Count ({c.Count}) < Values.Length ({c.Values.Length})");
                issues++;
            }

            return issues;
        }

        private static bool IsStrictlyAscending(double[] a)
        {
            for (int i = 1; i < a.Length; i++)
                if (!(a[i] > a[i - 1])) return false;
            return true;
        }

        private static bool IsNonDecreasing(double[] a)
        {
            for (int i = 1; i < a.Length; i++)
                if (a[i] < a[i - 1]) return false;
            return true;
        }
    }
}
