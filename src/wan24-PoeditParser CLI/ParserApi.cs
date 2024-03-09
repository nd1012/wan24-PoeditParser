﻿using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Text;
using wan24.CLI;
using wan24.Core;
using static wan24.Core.Logging;
using static wan24.Core.Logger;
using System.Reflection;

namespace wan24.PoeditParser
{
    /// <summary>
    /// Poedit parser CLI API
    /// </summary>
    [CliApi("parser", IsDefault = true)]
    [DisplayText("Poedit parser")]
    [Description("CLI API for parsing a PO file from source code")]
    public sealed class ParserApi
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ParserApi() { }

        /// <summary>
        /// Parse source code
        /// </summary>
        /// <param name="config">Custom configuration file</param>
        /// <param name="singleThread">Disable multi-threading?</param>
        /// <param name="verbose">Be verbose?</param>
        /// <param name="noRecursive">Disable directory recursion?</param>
        /// <param name="ext">File extensions to use (including dot)</param>
        /// <param name="encoding">Source text encoding identifier</param>
        /// <param name="input">Input file-/foldernames (may be relative or absolute)</param>
        /// <param name="exclude">Excluded file-/foldernames (absolute path or filename only)</param>
        /// <param name="output">Output PO filename (may be relative or absolute)</param>
        /// <param name="noHeader">Skip writing a header</param>
        [CliApi("parse", IsDefault = true)]
        [DisplayText("Parse")]
        [Description("Parses a PO file from source code")]
        [StdIn("/path/to/source.cs")]
        [StdOut("/path/to/target.po")]
        public static async Task ParseAsync(
            [CliApi(Example = "/path/to/config.json")]
            [DisplayText("Configuration")]
            [Description("Path to the custom configuration JSON file to use (may be relative or absolute)")]
            string? config = null,
            [CliApi]
            [DisplayText("Single threaded")]
            [Description("Disable multi-threading (process only one source file per time)")]
            bool singleThread = false,
            [CliApi]
            [DisplayText("Verbose")]
            [Description("Write processing details to STDERR (multi-threading will be disabled)")]
            bool verbose = false,
            [CliApi]
            [DisplayText("No recursive")]
            [Description("Don't recurse into sub-folders (top directory only)")]
            bool noRecursive = false,
            [CliApi]
            [DisplayText("Extensions")]
            [Description("File extensions to look for (including dot)")]
            string[]? ext = null,
            [CliApi(Example = "UTF-8")]
            [DisplayText("Encoding")]
            [Description("Text encoding of the source files (may be any encoding identifier)")]
            string? encoding = null,
            [CliApi(Example = "/path/to/source.cs")]
            [DisplayText("Source files/folders")]
            [Description("Path to source files and folders (may be relative or absolute)")]
            string[]? input = null,
            [CliApi(Example = "/path/to/source/sub/folder")]
            [DisplayText("Exclude files/folders")]
            [Description("Path to excluded source files and folders (absolute path or filename only)")]
            string[]? exclude = null,
            [CliApi(Example = "/path/to/output.po")]
            [DisplayText("Output path")]
            [Description("Path to the output PO file (may be relative or absolute)")]
            string? output = null,
            [CliApi]
            [DisplayText("No header")]
            [Description("Skip writing a header to the PO file")]
            bool noHeader = false
            )
        {
            // Configure
            if (config is not null) await AppConfig.LoadAsync<ParserAppConfig>(config).DynamicContext();
            if (singleThread || verbose) ParserConfig.SingleThread = verbose || singleThread;
            if (ext is not null && ext.Length > 0)
            {
                ParserConfig.FileExtensions.Clear();
                ParserConfig.FileExtensions.AddRange(ext);
            }
            if (encoding is not null) ParserConfig.SourceEncoding = Encoding.GetEncoding(encoding);
            if (verbose) Logging.Logger ??= new VividConsoleLogger(LogLevel.Information);
            if (!verbose) verbose = Logging.Logger is not null && Info;
            if (verbose)
            {
                WriteInfo($"Multithreading: {!ParserConfig.SingleThread}");
                WriteInfo($"Source encoding: {ParserConfig.SourceEncoding.EncodingName}");
                WriteInfo($"Patterns: {ParserConfig.Patterns.Count}");
                WriteInfo($"File extensions: {string.Join(", ", ParserConfig.FileExtensions)}");
            }
            // Process
            HashSet<ParserMatch> keywords = [];
            if (input is null)
            {
                // Use STDIN
                if (verbose) WriteInfo("Using STDIN");
                await ProcessAsync(Console.OpenStandardInput(), keywords, verbose).DynamicContext();
            }
            else
            {
                // Use given file-/foldernames
                if (verbose) WriteInfo("Using given file-/foldernames");
                TranslationWorker worker = new(ParserConfig.SingleThread ? 1 : Environment.ProcessorCount << 1, keywords, verbose);
                await using (worker.DynamicContext())
                {
                    await worker.StartAsync().DynamicContext();
                    string[] extensions = [.. ParserConfig.FileExtensions],
                        files;
                    string fullPath;
                    foreach (string path in input)
                    {
                        fullPath = Path.GetFullPath(path);
                        if (Directory.Exists(fullPath))
                        {
                            if (exclude is not null && exclude.Contains(fullPath))
                            {
                                if (verbose) WriteInfo($"Folder {fullPath} was excluded");
                                continue;
                            }
                            files = FsHelper.FindFiles(fullPath, recursive: !noRecursive, extensions: extensions).ToArray();
                            if (files.Length < 1)
                            {
                                if (verbose) WriteInfo($"Found no files in {fullPath}");
                                continue;
                            }
                            if (verbose) WriteInfo($"Found {files.Length} files in {fullPath}");
                            await worker.EnqueueRangeAsync(files).DynamicContext();
                        }
                        else if (File.Exists(fullPath))
                        {
                            if (exclude is not null && (exclude.Contains(fullPath) || exclude.Contains(Path.GetFileName(fullPath))))
                            {
                                if (verbose) WriteInfo($"File {fullPath} was excluded");
                                continue;
                            }
                            if (verbose) WriteInfo($"Add file {fullPath}");
                            await worker.EnqueueAsync(fullPath).DynamicContext();
                        }
                        else
                        {
                            throw new FileNotFoundException("The given path wasn't found", fullPath);
                        }
                    }
                    await worker.WaitBoringAsync().DynamicContext();
                }
            }
            if (verbose) WriteInfo("Done processing input source files");
            // Write output
            Stream outputStream = output is null
                ? Console.OpenStandardOutput()
                : FsHelper.CreateFileStream(output, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
            await using (outputStream.DynamicContext())
            {
                using StreamWriter writer = new(outputStream, Encoding.UTF8);
                // Header
                if (!noHeader)
                {
                    if (verbose) WriteInfo("Writing PO header");
                    await writer.WriteLineAsync($"# PO file created using wan24PoeditParser").DynamicContext();
                    await writer.WriteLineAsync($"# https://github.com/nd1012/wan24-PoeditParser").DynamicContext();
                    await writer.WriteLineAsync($"#").DynamicContext();
                    await writer.WriteLineAsync($"msgid \"\"").DynamicContext();
                    await writer.WriteLineAsync($"msgstr \"\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"MIME-Version: 1.0\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"Content-Type: text/plain; charset=UTF-8\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"Content -Transfer-Encoding: 8bit\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{$"X-Generator: wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{$"X-Poedit-SourceCharset: {ParserConfig.SourceEncoding.WebName}\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                }
                // Found keywords
                foreach (ParserMatch match in keywords)
                {
                    if (verbose) WriteInfo($"Writing keyword \"{match.Keyword}\"");
                    await WriteEntryAsync(writer, match).DynamicContext();
                }
            }
        }

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
            if(verbose) WriteInfo($"Processing file {fileName}");
            bool found = false;
            await using (stream.DynamicContext())
            {
                bool replaced;
                string currentLine,
                    keyword;
                int lineNumber = 0;
                ParserMatch? match;
                ParserPattern? pattern;
                using StreamReader reader = new(stream, ParserConfig.SourceEncoding);
                while (await reader.ReadLineAsync().DynamicContext() is string line)
                {
                    lineNumber++;
                    currentLine = keyword = line;
                    while (true)
                    {
                        pattern = ParserConfig.Patterns.FirstOrDefault(p => p.Expression.IsMatch(currentLine));
                        if (pattern is null) break;
                        found = true;
                        replaced = true;
                        keyword = currentLine;
                        while (replaced)
                        {
                            replaced = false;
                            foreach (ParserPattern pp in ParserConfig.Patterns.Where(p => p.Replacement is not null))
                            {
                                if (!pp.Expression.IsMatch(keyword)) continue;
                                replaced = true;
                                keyword = pp.Expression.Replace(keyword, pp.Replacement!);
                            }
                        }
                        currentLine = currentLine.Replace(pattern.Expression.Replace(currentLine, "$1"), string.Empty);
                        keyword = JsonHelper.Decode<string>(keyword)!;
                        lock (keywords)
                        {
                            match = keywords.FirstOrDefault(m => m.Keyword == keyword);
                            if (match is null)
                                keywords.Add(match = new()
                                {
                                    Keyword = keyword,
                                });
                            match.Positions.Add(new()
                            {
                                FileName = fileName,
                                LineNumber = lineNumber
                            });
                        }
                        if (verbose) WriteInfo($"Found keyword \"{keyword}\" in file{(fileName is null ? string.Empty : $" \"{fileName}\"")} on line #{lineNumber}");
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
            await stream.WriteLineAsync().DynamicContext();
            await stream.WriteAsync($"#:").DynamicContext();
            foreach (ParserMatch.Position pos in match.Positions)
                await stream.WriteAsync($" {((pos.FileName?.Contains(' ') ?? true) ? $"\u2068{pos.FileName}\u2069" : pos.FileName)}:{pos.LineNumber}").DynamicContext();
            await stream.WriteLineAsync().DynamicContext();
            if (match.Keyword.Contains('\n'))
            {
                await stream.WriteLineAsync($"msgid \"\"");
                foreach (string line in match.Keyword.Replace("\r", string.Empty).Split('\n'))
                    await stream.WriteLineAsync($"\"{$"{line}\\n".ToPoeditMessageLiteral()}\"");
            }
            else
            {
                await stream.WriteLineAsync($"msgid \"{match.Keyword.ToPoeditMessageLiteral()}\"");
            }
            await stream.WriteLineAsync($"msgstr \"\"");
        }

        /// <summary>
        /// Translation worker
        /// </summary>
        /// <remarks>
        /// Constructor
        /// </remarks>
        /// <param name="capacity">Capacity</param>
        /// <param name="keywords">Keywords</param>
        /// <param name="verbose">Be verbose?</param>
        private sealed class TranslationWorker(in int capacity, in HashSet<ParserMatch> keywords, in bool verbose)
            : ParallelItemQueueWorkerBase<string>(capacity, capacity)
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
                FileStream fs = FsHelper.CreateFileStream(item, FileMode.Open, FileAccess.Read, FileShare.Read);
                await using (fs.DynamicContext()) await ProcessAsync(fs, Keywords, Verbose, item).DynamicContext();
            }
        }
    }
}
