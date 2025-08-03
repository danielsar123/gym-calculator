namespace GymCalculator.DataGenerator.Models
{
    public class TestGroupMetrics
    {
        /// <summary>
        /// Keys are equipment names (“all”, “raw”, “single‐ply”, …).
        /// Each container has its own Count plus the three raw‐lift curves.
        /// </summary>
        public Dictionary<string, EquipmentMetrics> Equipment { get; set; }
    }

}
