namespace GymCalculator.DataGenerator.Utilities
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(
            this Dictionary<TKey, TValue> dict,
            TKey key,
            Func<TValue> valueFactory)
        {
            if (!dict.TryGetValue(key, out var val))
            {
                val = valueFactory();
                dict[key] = val;
            }
            return val;
        }
    }
}
