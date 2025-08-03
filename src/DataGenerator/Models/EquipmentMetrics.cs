namespace GymCalculator.DataGenerator.Models
{
    public class EquipmentMetrics
    {
        /// <summary>How many lifters in exactly this equip+tested group.</summary>
        public int Count { get; set; }

        public MetricCurve? Bench { get; set; }
        public MetricCurve? Squat { get; set; }
        public MetricCurve? Deadlift { get; set; }
    }
}
