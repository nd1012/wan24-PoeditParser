namespace wan24.PoeditParser
{
    /// <summary>
    /// Parser match
    /// </summary>
    public sealed record class ParserMatch
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ParserMatch() { }

        /// <summary>
        /// Keyword
        /// </summary>
        public required string Keyword { get; init; }

        /// <summary>
        /// Positions
        /// </summary>
        public HashSet<Position> Positions { get; } = [];

        /// <summary>
        /// Match position
        /// </summary>
        public sealed record class Position
        {
            /// <summary>
            /// Constructor
            /// </summary>
            public Position() { }

            /// <summary>
            /// Filename
            /// </summary>
            public string? FileName { get; init; }

            /// <summary>
            /// Line number
            /// </summary>
            public required int LineNumber { get; init; }
        }
    }
}
