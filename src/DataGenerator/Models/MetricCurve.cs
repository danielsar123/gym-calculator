namespace GymCalculator.DataGenerator.Models
{
    /// <summary>
    /// Count of lifters in this metric (including duplicates)
    /// A pair of parallel arrays: Values[i] maps to Pcts[i].
    /// </summary>
    public class MetricCurve
    {
        public int Count { get; set; }
        public double[] Values { get; set; }
        public double[] Pcts { get; set; }
    }

}
