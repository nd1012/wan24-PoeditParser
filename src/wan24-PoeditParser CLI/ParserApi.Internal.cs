using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;
using System.Text.RegularExpressions;
using wan24.Core;
using static wan24.Core.Logger;
using static wan24.Core.Logging;

namespace wan24.PoeditParser
{
    // Internal
    public sealed partial class ParserApi
    {
        /// <summary>
        /// Process a source file
        /// </summary>
        /// <param name="stream">Stream (will be disposed!)</param>
        /// <param name="keywords">Keywords</param>
        /// <param name="verbose">Be verbose?</param>
        /// <param name="fileName">Filename</param>
        private static async Task ProcessFileAsync(
            Stream stream,
            HashSet<ParserMatch> keywords,
            bool verbose,
            string? fileName = null
            )
        {
            if (verbose) WriteInfo($"Processing source file \"{fileName}\"");
            await using (stream.DynamicContext())
            {
                bool replaced;// If a replace pattern did match during the replace loop
                string currentLine,// Current line in the source file (without the last matched patterns)
                    keyword;// Currently matched keyword
                int lineNumber = 0;// Current line number in the source file (starts with 1)
                ParserMatch? match;// Existing/new Poedit parser match
                ParserPattern? pattern;// First matching Poedit parser pattern (may be a matching pattern or a replacement)
                Match? rxMatch = null;// Regular expression match of the first matching Poedit parser pattern
                using StreamReader reader = new(stream, ParserConfig.SourceEncoding);// Source file reader which uses the configured source encoding
                while (await reader.ReadLineAsync().DynamicContext() is string line)
                {
                    // File contents per line loop
                    lineNumber++;
                    if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber}");
                    if (line.Trim() == string.Empty)
                    {
                        if (Trace) WriteTrace($"Skipping empty source file \"{fileName}\" line #{lineNumber}");
                        continue;
                    }
                    currentLine = keyword = line;
                    while (true)
                    {
                        // Current line parsing loop (parse until no Poedit parser pattern is matching)
                        pattern = ParserConfig.Patterns
                            .FirstOrDefault(p => p.Replacement is null && (rxMatch = p.Expression.Matches(currentLine).FirstOrDefault()) is not null);
                        if (pattern is null)
                        {
                            if (Trace) WriteTrace($"No pattern matching for source file \"{fileName}\" line #{lineNumber}");
                            break;
                        }
                        // Handle the current match
                        Contract.Assert(rxMatch is not null);
                        if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} pattern \"{pattern.Pattern}\" matched \"{rxMatch.Groups[1].Value}\"");
                        replaced = true;
                        keyword = currentLine;
                        while (replaced)
                        {
                            // Poedit parser pattern loop (replace until we have the final keyword)
                            replaced = false;
                            foreach (ParserPattern replace in ParserConfig.Patterns.Where(p => p.Replacement is not null && p.Expression.IsMatch(keyword)))
                            {
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} replacement pattern \"{replace.Pattern}\" matched \"{keyword}\"");
                                replaced = true;
                                keyword = replace.Expression.Replace(keyword, replace.Replacement!);
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} replacement pattern \"{replace.Pattern}\" replaced to \"{keyword}\"");
                            }
                        }
                        // Remove the parsed keyword from the current line and store its position
                        currentLine = currentLine.Replace(keyword, string.Empty);
                        if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} new line is \"{currentLine}\" after keyword \"{keyword}\" was extracted");
                        // Decode the parsed keyword literal to a string
                        keyword = keyword.Trim();
                        if (keyword.StartsWith('\'')) keyword = $"\"{keyword[1..]}";
                        if (keyword.EndsWith('\'')) keyword = $"{keyword[..^1]}\"";
                        if (!keyword.StartsWith('\"') || !keyword.EndsWith('\"'))
                        {
                            WriteError($"Source file \"{fileName}\" line #{lineNumber} keyword \"{keyword}\" is not a valid string literal (regular expression pattern failure)");
                            if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
                            FailOnErrorIfRequested();
                            continue;
                        }
                        try
                        {
                            keyword = JsonHelper.Decode<string>(keyword) ?? throw new InvalidDataException($"Failed to decode keyword \"{keyword}\"");// keyword = "message"
                        }
                        catch(Exception ex)
                        {
                            WriteError($"Source file \"{fileName}\" line #{lineNumber} keyword \"{keyword.ToLiteral()}\" failed to decode to string: ({ex.GetType()}) {ex.Message}");
                            if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
                            FailOnErrorIfRequested();
                            continue;
                        }
                        // Store the parsed keyword (position)
                        lock (keywords)
                        {
                            match = keywords.FirstOrDefault(m => m.Keyword == keyword);
                            if (match is null)
                            {
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} new keyword \"{keyword.ToLiteral()}\"");
                                keywords.Add(match = new()
                                {
                                    Keyword = keyword,
                                });
                            }
                            else if (Trace)
                            {
                                WriteTrace($"Source file \"{fileName}\" line #{lineNumber} existing keyword \"{match.KeywordLiteral}\"");
                            }
                            match.Positions.Add(new()
                            {
                                FileName = fileName,
                                LineNumber = lineNumber
                            });
                        }
                        if (verbose) WriteInfo($"Found keyword \"{match.KeywordLiteral}\" in source {(fileName is null ? string.Empty : $" file \"{fileName}\"")} on line #{lineNumber}");
                    }
                }
            }
        }

        /// <summary>
        /// Fail on error, if requested
        /// </summary>
        private static void FailOnErrorIfRequested()
        {
            if (FailOnError || ParserConfig.FailOnError)
                throw new InvalidDataException("Forced to fail in total on any error");
        }

        /// <summary>
        /// File worker
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="capacity">Capacity</param>
        /// <param name="keywords">Keywords</param>
        /// <param name="verbose">Be verbose?</param>
        private sealed class ParallelFileWorker(in int capacity, in HashSet<ParserMatch> keywords, in bool verbose)
            : ParallelItemQueueWorkerBase<string>(capacity, threads: capacity)
        {
            /// <summary>
            /// Keywords
            /// </summary>
            public HashSet<ParserMatch> Keywords { get; } = keywords;

            /// <summary>
            /// Verbose?
            /// </summary>
            public bool Verbose { get; } = verbose;

            /// <inheritdoc/>
            protected override async Task ProcessItem(string item, CancellationToken cancellationToken)
            {
                if (Trace) WriteTrace($"Now going to process source file \"{item}\"");
                FileStream fs = FsHelper.CreateFileStream(item, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using (fs.DynamicContext()) await ProcessFileAsync(fs, Keywords, Verbose, item).DynamicContext();
            }
        }

        /// <summary>
        /// Parser excluding helper
        /// </summary>
        private sealed partial class ParserExcludes
        {
            /// <summary>
            /// Regular expression to match a windows path
            /// </summary>
            private static readonly Regex RxWindowsPath = RxWindowsPath_Generator();
            /// <summary>
            /// Regular expression to match a one or many wildcard (<c>$1</c> is the prefix, <c>$2</c> the postfix)
            /// </summary>
            private static readonly Regex RxOneOrMany = RxOneOrMany_Generator();

            /// <summary>
            /// Excluded names
            /// </summary>
            private readonly FrozenSet<string>? ExcludedNames;
            /// <summary>
            /// Excluded partials
            /// </summary>
            private readonly FrozenSet<string>? ExcludedPartials;
            /// <summary>
            /// Excluded paths
            /// </summary>
            private readonly FrozenSet<string>? ExcludedPaths;
            /// <summary>
            /// Excluding expression
            /// </summary>
            private readonly Regex? Expression;
            /// <summary>
            /// Any exclusions?
            /// </summary>
            private readonly bool AnyExclusions;
            /// <summary>
            /// String comparer
            /// </summary>
            private readonly NameComparer? Comparer;

            /// <summary>
            /// Constructor
            /// </summary>
            /// <param name="exclude">Exclusions</param>
            public ParserExcludes(in string[] exclude)
            {
                if (exclude.Length > 0)
                {
                    if (Trace) WriteTrace($"Handling {exclude.Length} exclusions");
                    HashSet<string> rx = [],
                        names = [],
                        partials = [],
                        paths = [];
                    foreach (string expression in exclude)
                        if (expression.Contains('*'))
                        {
                            if (Trace) WriteTrace($"Adding \"{expression}\" to the exclude expression");
                            rx.Add(expression);
                        }
                        else if (expression.StartsWith('/') || RxWindowsPath.IsMatch(expression))
                        {
                            if (Trace) WriteTrace($"Adding \"{expression}\" to the exclude paths");
                            paths.Add($"{Path.GetFullPath(expression)}{(ENV.IsWindows ? "\\" : "/")}");
                        }
                        else if (expression.ContainsAny('/', '\\'))
                        {
                            if (Trace) WriteTrace($"Adding \"{expression}\" to the exclude partials");
                            partials.Add(expression);
                        }
                        else
                        {
                            if (Trace) WriteTrace($"Adding \"{expression}\" to the exclude names");
                            names.Add(expression);
                        }
                    if (names.Count > 0) ExcludedNames = names.ToFrozenSet();
                    if (partials.Count > 0) ExcludedPartials = partials.ToFrozenSet();
                    if (paths.Count > 0) ExcludedPaths = paths.ToFrozenSet();
                    if (rx.Count > 0)
                    {
                        string regex = Regex.Escape(string.Join('|', rx)).Replace("\\*", ".*").Replace("\\|", "|");
                        regex = RxOneOrMany.Replace(regex, "$1.$2");
                        Expression = new(regex, RegexOptions.Compiled | RegexOptions.IgnoreCase);
                    }
                    else
                    {
                        Expression = null;
                    }
                    Comparer = new();
                    AnyExclusions = true;
                }
                else
                {
                    if (Trace) WriteTrace("No paths will be excluded");
                    ExcludedNames = null;
                    ExcludedPartials = null;
                    ExcludedPaths = null;
                    Expression = null;
                    Comparer = null;
                    AnyExclusions = false;
                }
            }

            /// <summary>
            /// Determine if a path is excluded
            /// </summary>
            /// <param name="path">Path</param>
            /// <returns>Is excluded?</returns>
            public bool IsPathExcluded(string path)
                => AnyExclusions &&
                    (
                        !(ExcludedNames?.Contains(Path.GetFileName(path), Comparer) ?? false) ||
                        !(ExcludedPaths?.Any(p => path.StartsWith(path, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        !(ExcludedPartials?.Any(p => path.Contains(p, StringComparison.OrdinalIgnoreCase)) ?? false) ||
                        !(Expression?.IsMatch(path) ?? false)
                    );

            /// <summary>
            /// Name comparer
            /// </summary>
            private readonly struct NameComparer : IEqualityComparer<string>
            {
                /// <summary>
                /// Constructor
                /// </summary>
                public NameComparer() { }

                /// <inheritdoc/>
                public bool Equals(string? x, string? y)
                    => (x is null && y is null) ||
                        (
                            x is not null &&
                            y is not null &&
                            x.Length == y.Length &&
                            x.Equals(y, StringComparison.OrdinalIgnoreCase)
                        );

                /// <inheritdoc/>
                public int GetHashCode([DisallowNull] string obj) => obj.ToLower().GetHashCode();
            }

            /// <summary>
            /// Regular expression to match a windows path
            /// </summary>
            /// <returns>Regular expression</returns>
            [GeneratedRegex(@"^[a-z]\:?[\/\\]", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline, "de-DE")]
            private static partial Regex RxWindowsPath_Generator();

            /// <summary>
            /// Regular expression to match a one or many wildcard (<c>$1</c> is the prefix, <c>$2</c> the postfix)
            /// </summary>
            /// <returns>Regular expression</returns>
            [GeneratedRegex(@"^(.*[^\\])\\(\+.*)$", RegexOptions.Compiled)]
            private static partial Regex RxOneOrMany_Generator();
        }
    }
}
