using System;
using System.Linq;
using FluentAssertions;
using GymCalculator.Utilities;
using Xunit;

namespace GymCalculator.Tests.Unit
{
    [Trait("Category", "Unit")]
    public class PercentileCalculatorTests
    {
        private static double[] RoundAll(double[] xs, int dp = 2) =>
            xs.Select(v => Math.Round(v, dp, MidpointRounding.AwayFromZero)).ToArray();

        [Theory]
        // Unique values: N=3 -> ECDF(≤): [1/3, 2/3, 1] => [33.33, 66.67, 100.00]
        [InlineData(new double[] { 10, 20, 30 },
                    new double[] { 10, 20, 30 },
                    new double[] { 33.33, 66.67, 100.00 })]

        // Duplicates preserved via frequency: [10,10,20,30]; N=4
        // ECDF(≤): 10 => 2/4=50, 20 => 3/4=75, 30 => 4/4=100
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
        public void BuildMetricArrays_Produces_Correct_DistinctValues_And_ECDF(
            double[] input,
            double[] expectedValues,
            double[] expectedPcts)
        {
            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(input);

            vals.Should().Equal(expectedValues);

            // Round returned percentiles to match configured rounding behavior
            RoundAll(pcts).Should().Equal(expectedPcts);
        }

        [Fact]
        public void EvaluatePercentile_Handles_Bounds_And_Inclusive_Lookup_Correctly()
        {
            // Build ECDF from simple dataset
            var (vals, pcts) = PercentileCalculator.BuildMetricArrays(new[] { 10d, 20d, 30d });

            // Below smallest value -> 0%
            PercentileCalculator.EvaluatePercentile(vals, pcts, 5)
                .Should().Be(0.00);

            // Exact matches -> inclusive ECDF value
            PercentileCalculator.EvaluatePercentile(vals, pcts, 10)
                .Should().Be(33.33);

            PercentileCalculator.EvaluatePercentile(vals, pcts, 20)
                .Should().Be(66.67);

            PercentileCalculator.EvaluatePercentile(vals, pcts, 30)
                .Should().Be(100.00);

            // Between breakpoints -> floor to right-most ≤ x
            PercentileCalculator.EvaluatePercentile(vals, pcts, 15)
                .Should().Be(33.33);

            PercentileCalculator.EvaluatePercentile(vals, pcts, 25)
                .Should().Be(66.67);

            // Above largest value -> 100%
            PercentileCalculator.EvaluatePercentile(vals, pcts, 100)
                .Should().Be(100.00);
        }

        [Fact]
        public void EvaluatePercentile_Returns_Zero_For_Empty_Input()
        {
            PercentileCalculator.EvaluatePercentile(
                Array.Empty<double>(),
                Array.Empty<double>(),
                50
            ).Should().Be(0.0);
        }
    }
}
