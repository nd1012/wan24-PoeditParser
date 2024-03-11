using System.Text.RegularExpressions;

namespace wan24.PoeditParser
{
    /// <summary>
    /// Poedit parser pattern
    /// </summary>
    public sealed record class ParserPattern
    {
        /// <summary>
        /// Regular expression
        /// </summary>
        private Regex? _Expression = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public ParserPattern() { }

        /// <summary>
        /// Regular expression pattern
        /// </summary>
        public required string Pattern { get; init; }

        /// <summary>
        /// Regular expression options
        /// </summary>
        public RegexOptions Options { get; init; } = RegexOptions.None;

        /// <summary>
        /// Regular expression
        /// </summary>
        public Regex Expression => _Expression ??= new(Pattern, RegexOptions.Compiled | RegexOptions.Singleline | Options);

        /// <summary>
        /// Replacement pattern
        /// </summary>
        public string? Replacement { get; init; }
    }
}
