namespace GymCalculator.Utilities
{
    /// <summary>
    /// Provides utilities to compute and query percentiles using the
    /// Empirical Cumulative Distribution Function (ECDF).
    /// - Percentiles are defined inclusively:
    ///   For a given lifter’s score x, the percentile is the
    ///   proportion of all scores X in the dataset that are
    ///   less than or equal to x (P(X ≤ x)).
    /// - Equal values (ties) receive the same percentile.
    /// - Top performer(s) receive 100%, bottom performer(s) > 0% (unless the dataset is empty).
    /// 
    /// Intended for use with powerlifting metrics (DOTS, Wilks, totals, etc.)
    /// where duplicate values are common and should be credited equally.
    /// </summary>
    public static class PercentileCalculator
    {
        // Centralized precision + rounding mode for percentiles.
        // Change these here to affect both ECDF construction and lookups.
        private const int PctPrecision = 2;
        private const MidpointRounding RoundMode = MidpointRounding.AwayFromZero;

        /// <summary>
        /// Builds parallel arrays of distinct positive values and their inclusive ECDF percentiles.
        /// 
        /// Example:
        ///   rawValues = [10,10,20,30]
        ///   => Values = [10,20,30]
        ///   => Pcts   = [50.00,75.00,100.00]
        /// 
        /// Notes:
        /// - Values ≤ 0 are filtered out.
        /// - Duplicate values are grouped, and their frequency contributes
        ///   to the cumulative step size in the ECDF.
        /// - Percentiles are not rounded here (stored raw), but may be rounded
        ///   later when displayed or looked up.
        /// </summary>
        public static (double[] Values, double[] Pcts) BuildMetricArrays(IEnumerable<double> rawValues)
        {
            var groups = rawValues
                .Where(v => v > 0)  // ignore non-positive lifts
                .Select(v=> Math.Round(v, 2))
                .GroupBy(v => v)
                .Select(g => new { Value = g.Key, Count = g.Count() })
                .OrderBy(g => g.Value)
                .ToArray();

            int total = groups.Sum(g => g.Count);
            if (total == 0)
                return (Array.Empty<double>(), Array.Empty<double>());

            var vals = new double[groups.Length];
            var pcts = new double[groups.Length];

            int cum = 0; // running cumulative count of observations
            for (int i = 0; i < groups.Length; i++)
            {
                cum += groups[i].Count;

                vals[i] = groups[i].Value;
                // ECDF "upper cumulative": proportion ≤ this value
                // Example: if 75% of lifters have ≤ this DOTS,
                // the percentile is 75%.
                pcts[i] = 100.0 * cum / total;
            }

            return (vals, pcts);
        }

        /// <summary>
        /// Returns the percentile for a given lift value x.
        /// 
        /// Behavior:
        /// - If x matches a stored value, returns its percentile (inclusive: ≤).
        /// - If x falls between values, returns the percentile of the nearest
        ///   lower value (right-most ≤ x).
        /// - If x is below the smallest value, returns 0.0.
        /// - If x is above the largest value, returns ~100%.
        /// 
        /// Percentiles are rounded to the configured precision.
        /// </summary>
        public static double EvaluatePercentile(double[] values, double[] pcts, double x)
        {
            if (values == null || pcts == null || values.Length == 0)
                return 0.0;

            // values: sorted ascending unique metric values
            // pcts:   inclusive ECDF P(X ≤ value) in percent, parallel to values

            int idx = Array.BinarySearch(values, x);

            if (idx < 0)
            {
                // Not an exact match: ~idx is the insertion point.
                // We want the nearest value strictly less than x.
                idx = ~idx - 1;

                // If x is smaller than the minimum observed value,
                // percentile is defined as 0%.
                if (idx < 0)
                    return 0.0;
            }

            // If x ≥ max observed value, idx will be the last index,
            // so pcts[idx] will naturally be ~100%.
            return Math.Round(pcts[idx], PctPrecision, RoundMode);
        }
    }
}
