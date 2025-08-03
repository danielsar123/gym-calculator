using CsvHelper.Configuration.Attributes;

namespace GymCalculator.DataGenerator.Models
{
    #nullable disable
    public class LifterRecord
    {
        [Name("Place")] public string Place { get; set; }
        [Name("AgeClass")] public string AgeClass { get; set; }
        [Name("Sex")] public string Sex { get; set; }
        [Name("WeightClassKg")] public string WeightClassKg { get; set; }
        [Name("Tested")] public string Tested { get; set; }
        [Name("Equipment")] public string Equipment { get; set; }
        [Name("Best3BenchKg")] public double? Best3BenchKg { get; set; }
        [Name("Best3SquatKg")] public double? Best3SquatKg { get; set; }
        [Name("Best3DeadliftKg")] public double? Best3DeadliftKg { get; set; }
        [Name("Dots")] public double? Dots { get; set; }
        [Name("Wilks")] public double? Wilks { get; set; }
    }
    #nullable enable
}
