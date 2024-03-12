namespace wan24.PoeditParser
{
    // Fuzzy keyword lookup helper
    public sealed partial class ParserApi
    {
        /// <summary>
        /// Fuzzy keyword lookup using Levenshtein distances
        /// </summary>
        /// <param name="newKeyword">New keyword</param>
        /// <param name="existingKeywords">Existing keywords</param>
        /// <param name="maxDistance">Maximum distance in percent</param>
        /// <returns>Best matching existing keyword</returns>
        private static string? FuzzyKeywordLookup(
            string newKeyword,
            in IEnumerable<string> existingKeywords,
            int maxDistance
            )
        {
            if (newKeyword.Length < 1) return null;
            newKeyword = newKeyword.ToLower();
            maxDistance = newKeyword.Length / 100 * maxDistance;
            return maxDistance < 1
                ? null
                : existingKeywords
                    .Where(keyword => keyword.Length > 0 && Math.Abs(newKeyword.Length - keyword.Length) <= maxDistance)
                    .Select(keyword => (keyword, LevenshteinDistance(newKeyword, keyword)))
                    .Where(info => info.Item2 <= maxDistance)
                    .OrderBy(info => info.Item2)
                    .Select(info => info.keyword)
                    .FirstOrDefault() ?? null;
        }

        /// <summary>
        /// Calculate the Levenshtein distance of two keywords
        /// </summary>
        /// <param name="newKeyword">New keyword (must be lower case)</param>
        /// <param name="existingKeyword">Existing keyword to compare</param>
        /// <returns>Levenshtein distance between the keywords</returns>
        private static int LevenshteinDistance(in string newKeyword, string existingKeyword)
        {
            // Special cases
            if (newKeyword.Length == existingKeyword.Length && newKeyword.Equals(existingKeyword, StringComparison.OrdinalIgnoreCase)) return 0;
            existingKeyword = existingKeyword.ToLower();
            // Initialize the matrix
            int[,] distances = new int[newKeyword.Length + 1, existingKeyword.Length + 1];
            for (int i = 0; i <= newKeyword.Length; distances[i, 0] = i, i++) ;
            for (int i = 0; i <= existingKeyword.Length; distances[0, i] = i, i++) ;
            // Calculate the distance
            for (int i = 1, j; i <= newKeyword.Length; i++)
                for (j = 1; j <= existingKeyword.Length; j++)
                    distances[i, j] = Math.Min(
                        Math.Min(
                            distances[i - 1, j] + 1,
                            distances[i, j - 1] + 1
                            ),
                        distances[i - 1, j - 1] + (newKeyword[i - 1] == existingKeyword[j - 1] ? 0 : 1)
                        );
            return distances[newKeyword.Length, existingKeyword.Length];
        }
    }
}
