using GymCalculator.DataGenerator.Models;

public class Bucket
{
    public List<LifterData> Tested { get; } = new();
    public List<LifterData> Untested { get; } = new();
}