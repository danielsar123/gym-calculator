namespace GymCalculator.DataGenerator.Models
{
    /// <summary>
    /// Top‐level file for one AgeClass.
    /// </summary>
    public class PrecomputedAgeClass
    {
        public Dictionary<string, GlobalMetrics> Global { get; set; }
        public Dictionary<string, SexNode> Sex { get; set; }
    }
}
