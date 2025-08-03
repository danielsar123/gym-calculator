using GymCalculator.DataGenerator.Models;

namespace GymCalculator.DataGenerator.Utilities
{
    public static class CsvExtensions
    {
        public static bool TestedEqualsYes(this LifterRecord r) =>
            string.Equals(r.Tested, "Yes", StringComparison.OrdinalIgnoreCase);

        public static IEnumerable<KeyValuePair<string, List<LifterData>>> GetEquipmentGroups(
            this Bucket b, bool tested)
        {
            var list = tested ? b.Tested : b.Untested;
            yield return new KeyValuePair<string, List<LifterData>>("all", list);
            foreach (var grp in list.GroupBy(ld => ld.Equipment))
                yield return new KeyValuePair<string, List<LifterData>>(grp.Key, grp.ToList());
        }
    }
}
