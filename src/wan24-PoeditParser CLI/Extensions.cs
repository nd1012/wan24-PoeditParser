using System.Collections.Frozen;

namespace wan24.PoeditParser
{
    /// <summary>
    /// Extensions
    /// </summary>
    internal static class Extensions
    {
        /// <summary>
        /// Literal string replacements
        /// </summary>
        private static readonly FrozenDictionary<string, string> LiteralReplacements;

        /// <summary>
        /// Constructor
        /// </summary>
        static Extensions()
        {
            LiteralReplacements = new Dictionary<string, string>()
            {
                {"\"", "\\\"" },
                {"\\", "\\" },
                {"\0", "\\0" },
                {"\a", "\\a" },
                {"\b", "\\b" },
                {"\f", "\\f" },
                {"\n", "\\n" },
                {"\r", "\\r" },
                {"\t", "\\t" },
                {"\v", "\\v" }
            }.ToFrozenDictionary();
        }

        /// <summary>
        /// Convert to a literal Poedit message string
        /// </summary>
        /// <param name="str">String</param>
        /// <returns>Literal string</returns>
        public static string ToPoeditMessageLiteral(this string str)
        {
            foreach (var kvp in LiteralReplacements) str = str.Replace(kvp.Key, kvp.Value);
            return str;
        }
    }
}
