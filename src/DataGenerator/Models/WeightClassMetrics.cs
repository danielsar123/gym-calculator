namespace GymCalculator.DataGenerator.Models
{
    /// <summary>
    /// Under a given sex + weight class
    /// </summary>
    public class WeightClassMetrics
    {
        public TestGroupMetrics Tested { get; set; }
        public TestGroupMetrics Untested { get; set; }
    }
}
