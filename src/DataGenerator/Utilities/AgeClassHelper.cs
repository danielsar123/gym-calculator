using System.Globalization;

namespace GymCalculator.Utilities
{
    public static class AgeClassHelper
    {
        /// <summary>
        /// Maps numeric age to canonical GymCalculator age buckets.
        /// </summary>
        public static string ToAgeClass(int age)
        {
            // Youth/Sub-Junior (Standardized)
            if (age <= 13) return "5-13";
            if (age <= 18) return "14-18"; // Official Sub-Junior range

            // Juniors (Standardized)
            if (age <= 23) return "19-23"; // Official Junior range

            // The "Prime" Years (Granular for better UX)
            if (age <= 29) return "24-29";
            if (age <= 34) return "30-34";
            if (age <= 39) return "35-39"; // Sub-Masters

            // Masters (Granular 5-year splits - keep these!)
            if (age <= 44) return "40-44";
            if (age <= 49) return "45-49";
            if (age <= 54) return "50-54";
            if (age <= 59) return "55-59";
            if (age <= 64) return "60-64";
            if (age <= 69) return "65-69";
            if (age <= 74) return "70-74";
            if (age <= 79) return "75-79";
            return "80-999";
        }
    }
}
