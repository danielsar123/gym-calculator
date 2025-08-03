using System;
using System.Linq;
using FluentAssertions;
using GymCalculator.Utilities;
using Xunit;

namespace GymCalculator.Tests.Utilities
{
    public class PercentileCalculatorTests
    {
        private static double[] RoundAll(double[] xs, int dp = 2) =>
            xs.Select(v => Math.Round(v, dp, MidpointRounding.AwayFromZero)).ToArray();

        [Theory]
        // Unique values: N=3 -> ECDF (≤): [1/3, 2/3, 1] => [33.33, 66.67, 100.00]
        [InlineData(new double[] { 10, 20, 30 },
                    new double[] { 10, 20, 30 },
                    new double[] { 33.33, 66.67, 100.00 })]

        // Duplicates kept via frequency: [10,10,20,30]; N=4
        // ECDF (≤): 10 => 2/4=50, 20 => 3/4=75, 30 => 4/4=100
        [InlineData(new double[] { 10, 10, 20, 30 },
                    new double[] { 10, 20, 30 },
                    new double[] { 50.00, 75.00, 100.00 })]

        // Non-positives filtered; only 5 remains: N=1 => 100%
        [InlineData(new double[] { 0, 0, 5 },
                    new double[] { 5 },
                    new double[] { 100.00 })]

        // Empty input -> empty outputs
        [InlineData(new double[] { },
                    new double[] { },
                    new double[] { })]
        public void BuildMetricArrays_Works_As_Expected(double[] input, double[] expectedValues, double[] expectedPcts)
        {
            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(input);

            vals.Should().Equal(expectedValues);

            // BuildMetricArrays returns raw doubles; round for assertion to avoid fp noise
            RoundAll(pcts).Should().Equal(expectedPcts);
        }

        [Fact]
        public void EvaluatePercentile_Clamps_And_Uses_RightMostLessOrEqual_Inclusive()
        {
            // For [10,20,30], ECDF(≤) percents are approximately:
            // 10 -> 33.33..., 20 -> 66.66..., 30 -> 100
            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(new[] { 10d, 20d, 30d });

            // below smallest -> 0%
            PercentileCalculator.EvaluatePercentile(vals, pcts, 5).Should().Be(0.00);

            // exact matches -> inclusive ECDF at that value
            PercentileCalculator.EvaluatePercentile(vals, pcts, 10).Should().Be(33.33);
            PercentileCalculator.EvaluatePercentile(vals, pcts, 20).Should().Be(66.67);
            PercentileCalculator.EvaluatePercentile(vals, pcts, 30).Should().Be(100.00);

            // between breakpoints -> floor to right-most ≤ x (i.e., lower neighbor)
            PercentileCalculator.EvaluatePercentile(vals, pcts, 15).Should().Be(33.33);
            PercentileCalculator.EvaluatePercentile(vals, pcts, 25).Should().Be(66.67);

            // above largest -> ~100%
            PercentileCalculator.EvaluatePercentile(vals, pcts, 100).Should().Be(100.00);
        }
    }
}
