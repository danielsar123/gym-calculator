namespace GymCalculator.DataGenerator.Models
{
    public class LifterData
    {
        public double Best3BenchKg { get; }
        public double Best3SquatKg { get; }
        public double Best3DeadliftKg { get; }
        public double Dots { get; }
        public double Wilks { get; }
        public string Equipment { get; }

        public double BenchPct { get; set; }
        public double SquatPct { get; set; }
        public double DeadliftPct { get; set; }
        public double DotsPct { get; set; }
        public double WilksPct { get; set; }

        public LifterData(LifterRecord r)
        {
            Best3BenchKg = r.Best3BenchKg ?? 0;
            Best3SquatKg = r.Best3SquatKg ?? 0;
            Best3DeadliftKg = r.Best3DeadliftKg ?? 0;
            Dots = r.Dots ?? 0;
            Wilks = r.Wilks ?? 0;
            Equipment = string.IsNullOrWhiteSpace(r.Equipment) ? "all" : r.Equipment;
        }
    }
}
