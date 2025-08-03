namespace GymCalculator.DataGenerator.Models
{
    /// <summary>
    /// “Global” Dots/Wilks section.
    /// </summary>
    public class GlobalMetrics
    {
        public int Count { get; set; }
        public MetricCurve? Dots { get; set; }
        public MetricCurve? Wilks { get; set; }
    }
}
