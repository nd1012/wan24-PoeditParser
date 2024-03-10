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
        /// <returns>If any keyword was found</returns>
        private static async Task<bool> ProcessAsync(
            Stream stream,
            HashSet<ParserMatch> keywords,
            bool verbose,
            string? fileName = null
            )
        {
            if (verbose) WriteInfo($"Processing source file \"{fileName}\"");
            bool found = false;// If any Poedit message was found in the current source file
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
                    currentLine = keyword = line;
                    while (true)
                    {
                        // Current line parsing loop (parse until no Poedit parser pattern is matching)
                        pattern = ParserConfig.Patterns.FirstOrDefault(p => (rxMatch = p.Expression.Matches(currentLine).FirstOrDefault()) is not null);
                        if (pattern is null)
                        {
                            if (Trace) WriteTrace($"No pattern matching for source file \"{fileName}\" line #{lineNumber}");
                            break;
                        }
                        // Handle the current match
                        Contract.Assert(rxMatch is not null);
                        if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} pattern \"{pattern.Pattern}\" matched \"{rxMatch.Groups[1].Value}\"");
                        found = true;
                        replaced = true;
                        keyword = currentLine;
                        while (replaced)
                        {
                            // Poedit parser pattern look (replace until we have the final keyword)
                            replaced = false;
                            foreach (ParserPattern pp in ParserConfig.Patterns.Where(p => p.Replacement is not null && p.Expression.IsMatch(keyword)))
                            {
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} replacement pattern \"{pp.Pattern}\" matched \"{keyword}\"");
                                replaced = true;
                                keyword = pp.Expression.Replace(keyword, pp.Replacement!);
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} replacement pattern \"{pp.Pattern}\" replaced to \"{keyword}\"");
                            }
                        }
                        // Remove the parsed keyword from the current line and store its position
                        currentLine = currentLine.Replace(rxMatch.Groups[1].Value, string.Empty);
                        if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} new line is \"{currentLine}\" after keyword \"{keyword}\" was extracted");
                        keyword = JsonHelper.Decode<string>(keyword) ?? throw new InvalidDataException($"Failed to decode keyword \"{keyword}\"");// keyword = "message"
                        lock (keywords)
                        {
                            match = keywords.FirstOrDefault(m => m.Keyword == keyword);
                            if (match is null)
                            {
                                if (Trace) WriteTrace($"Source file \"{fileName}\" line #{lineNumber} new keyword \"{keyword}\"");
                                keywords.Add(match = new()
                                {
                                    Keyword = keyword,
                                });
                            }
                            else if (Trace)
                            {
                                WriteTrace($"Source file \"{fileName}\" line #{lineNumber} existing keyword \"{keyword}\"");
                            }
                            match.Positions.Add(new()
                            {
                                FileName = fileName,
                                LineNumber = lineNumber
                            });
                        }
                        if (verbose) WriteInfo($"Found keyword \"{keyword}\" in source file{(fileName is null ? string.Empty : $" \"{fileName}\"")} on line #{lineNumber}");
                    }
                }
                return found;
            }
        }

        /// <summary>
        /// Write an entry
        /// </summary>
        /// <param name="stream">Stream</param>
        /// <param name="match">Match</param>
        private static async Task WriteEntryAsync(StreamWriter stream, ParserMatch match)
        {
            if (Trace) WriteTrace($"Keyword \"{match.Keyword}\" found at {match.Positions.Count} positions");
            await stream.WriteLineAsync().DynamicContext();
            // Position tags comment line
            await stream.WriteAsync($"#:").DynamicContext();
            foreach (ParserMatch.Position pos in match.Positions)
                await stream.WriteAsync($" {((pos.FileName?.Contains(' ') ?? true) ? $"\u2068{pos.FileName}\u2069" : pos.FileName)}:{pos.LineNumber}").DynamicContext();
            await stream.WriteLineAsync().DynamicContext();
            if (match.Keyword.Contains('\n'))
            {
                // Multiline message
                if (Trace) WriteTrace($"Keyword \"{match.Keyword}\" is multiline");
                await stream.WriteLineAsync($"msgid \"\"");
                foreach (string line in match.Keyword.Replace("\r", string.Empty).Split('\n'))
                    await stream.WriteLineAsync($"\"{$"{line}\n".ToPoeditMessageLiteral()}\"");
            }
            else
            {
                // Single line message
                if (Trace) WriteTrace($"Keyword \"{match.Keyword}\" is singleline");
                await stream.WriteLineAsync($"msgid \"{match.Keyword.ToPoeditMessageLiteral()}\"");
            }
            await stream.WriteLineAsync($"msgstr \"\"");
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
        private sealed class FileWorker(in int capacity, in HashSet<ParserMatch> keywords, in bool verbose)
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
                await using (fs.DynamicContext()) await ProcessAsync(fs, Keywords, Verbose, item).DynamicContext();
            }
        }
    }
}
