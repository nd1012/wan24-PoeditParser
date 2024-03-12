﻿using Karambolo.PO;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using System.Reflection;
using System.Text;
using wan24.CLI;
using wan24.Core;
using static wan24.Core.Logger;
using static wan24.Core.Logging;

namespace wan24.PoeditParser
{
    /// <summary>
    /// PO extractor CLI API
    /// </summary>
    [CliApi("extractor", IsDefault = true)]
    [DisplayText("PO extractor")]
    [Description("CLI API for creating/merging a PO file from source code extracted keywords")]
    public sealed partial class ParserApi
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ParserApi() { }

        /// <summary>
        /// Fail on error?
        /// </summary>
        [CliApi("failOnError")]
        [DisplayText("Fail on error")]
        [Description("Fail the whole process on any error")]
        public static bool FailOnError { get; set; }

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
        /// <param name="mergeOutput">Merge the PO output to the existing PO file</param>
        /// <param name="fuzzy">Maximum updated key Levenshtein distance in percent (only when merging; default is 10; set to zero to disable fuzzy matching)</param>
        [CliApi("extract", IsDefault = true)]
        [DisplayText("Extract")]
        [Description("Creates/merges a PO file from source code extracted keywords")]
        [StdIn("/path/to/source.cs")]
        [StdOut("/path/to/target.po")]
        public static async Task ExtractAsync(

            [CliApi(Example = "/path/to/config.json")]
            [DisplayText("Configuration")]
            [Description("Path to the custom configuration JSON file to use (may be relative or absolute, or only a filename to lookup; will look in current directory, app folder and temporary folder)")]
            string? config = null,

            [CliApi]
            [DisplayText("Single threaded")]
            [Description("Disable multi-threading (process only one source file per time)")]
            bool singleThread = false,

            [CliApi]
            [DisplayText("Verbose")]
            [Description("Log processing details (to STDERR; multi-threading will be disabled)")]
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
            [Description("Text encoding of the source files (may be any encoding (web) identifier)")]
            string? encoding = null,

            [CliApi(Example = "/path/to/source.cs")]
            [DisplayText("Source files/folders")]
            [Description("Path to source files and folders (may be relative or absolute)")]
            string[]? input = null,

            [CliApi(Example = "/path/to/source/sub/folder")]
            [DisplayText("Exclude files/folders")]
            [Description("Path to excluded source files and folders (absolute or partial path or file-/foldername only (\"*\" (any or none) and \"+\" (one or many) may be used as wildcard); case insensitive)")]
            string[]? exclude = null,

            [CliApi(Example = "/path/to/output.po")]
            [DisplayText("Output path")]
            [Description("Path to the output PO file (may be relative or absolute)")]
            string? output = null,

            [CliApi]
            [DisplayText("No header")]
            [Description("Skip adding header informations to a new PO file")]
            bool noHeader = false,

            [CliApi]
            [DisplayText("Merge output")]
            [Description("Merge the PO output to the existing output PO file")]
            bool mergeOutput = false,

            [CliApi(Example = "10", ParseJson = true)]
            [DisplayText("Fuzzy factor")]
            [Description("Maximum keyword Levenshtein distance in percent to update and mark an existing entry with the fuzzy flag (only when merging; default is 10; set to zero to disable fuzzy matching)")]
            int fuzzy = 10

            )
        {
            if (fuzzy < 0 || fuzzy > 100) throw new ArgumentOutOfRangeException(nameof(fuzzy));
            DateTime start = DateTime.Now;// Overall starting time
            // Configure
            string? configFn = null;// Finally used custom configuration filename
            if (config is not null)
            {
                // Load custom JSON configuration file
                if (Trace) WriteTrace($"Loading JSON configuration from \"{config}\"");
                if (FsHelper.FindFile(config, includeCurrentDirectory: true) is not string fn)
                    throw new FileNotFoundException("Configuration file not found", config);
                if (Trace && fn != config) WriteTrace($"Using configuration filename \"{fn}\"");
                configFn = fn;
                await AppConfig.LoadAsync<ParserAppConfig>(fn).DynamicContext();
            }
            if (singleThread || verbose)
            {
                // Override multithreading
                if (Trace) WriteTrace($"Single threaded {verbose || singleThread}");
                ParserConfig.SingleThread = verbose || singleThread;
            }
            if (ext is not null && ext.Length > 0)
            {
                // Override file extensions
                if (Trace) WriteTrace($"Override file extensions with \"{string.Join(", ", ext)}\"");
                ParserConfig.FileExtensions.Clear();
                ParserConfig.FileExtensions.AddRange(ext);
            }
            mergeOutput |= ParserConfig.MergeOutput;
            if (mergeOutput)
            {
                // Merge with the existing PO output file
                if (Trace) WriteTrace("Merge with existing PO output file (if any)");
                ParserConfig.MergeOutput = mergeOutput;
            }
            if (encoding is not null)
            {
                // Override source encoding
                if (Trace) WriteTrace($"Override source encoding with \"{encoding}\"");
                ParserConfig.SourceEncoding = Encoding.GetEncoding(encoding);
            }
            if (verbose) Logging.Logger ??= new VividConsoleLogger(LogLevel.Information);// Ensure having a logger for verbose output
            if (!verbose)
            {
                // Always be verbose if a logger was configured
                if (Trace) WriteTrace($"Force verbose {Logging.Logger is not null && Info}");
                verbose = Logging.Logger is not null && Info;
            }
            if (verbose)
            {
                // Output the used final settings
                WriteInfo($"Configuration file: {configFn ?? "(none)"}");
                WriteInfo($"Multi-threading: {!ParserConfig.SingleThread}");
                WriteInfo($"Source encoding: {ParserConfig.SourceEncoding.EncodingName}");
                WriteInfo($"Patterns: {ParserConfig.Patterns.Count}");
                WriteInfo($"File extensions: {string.Join(", ", ParserConfig.FileExtensions)}");
                WriteInfo($"Merge to output PO file: {ParserConfig.MergeOutput}");
                WriteInfo($"Fail on error: {FailOnError || ParserConfig.FailOnError}");
            }
            if (input is not null && ParserConfig.FileExtensions.Count < 1) throw new InvalidDataException("Missing file extensions to look for");
            if (!ParserConfig.Patterns.Any(p => p.Replacement is null)) throw new InvalidDataException("Missing matching-only patterns");
            if (!ParserConfig.Patterns.Any(p => p.Replacement is not null)) throw new InvalidDataException("Missing replace patterns");
            // Process
            DateTime started = DateTime.Now;// Part start time
            int sources = 0;// Number of source files parsed
            HashSet<ParserMatch> keywords = [];// Parsed keywords
            if (input is null)
            {
                // Use STDIN
                if (verbose) WriteInfo("Using STDIN");
                await ProcessFileAsync(Console.OpenStandardInput(), keywords, verbose).DynamicContext();
                sources++;
            }
            else
            {
                // Use given file-/foldernames
                if (verbose) WriteInfo("Using given file-/foldernames");
                if (input.Length < 1) throw new ArgumentException("Missing input locations", nameof(input));
                ParserExcludes excluding = new(exclude ?? []);
                ParallelFileWorker worker = new(ParserConfig.SingleThread ? 1 : Environment.ProcessorCount << 1, keywords, verbose)
                {
                    Name = "Poedit parser parallel file worker"
                };
                await using (worker.DynamicContext())
                {
                    // Start the parallel worker
                    await worker.StartAsync().DynamicContext();
                    string[] extensions = [.. ParserConfig.FileExtensions],// File extensions to look for
                        files;// Found files in an input source folder
                    string fullPath;// Full path of the current input source
                    foreach (string path in input)
                    {
                        // Process input file-/foldernames
                        if (Trace) WriteTrace($"Handling input path \"{path}\"");
                        fullPath = Path.GetFullPath(path);
                        if (Trace) WriteTrace($"Full input path for \"{path}\" is \"{fullPath}\"");
                        if (Directory.Exists(fullPath))
                        {
                            // Find files in a folder (optional recursive)
                            if (excluding.IsPathExcluded(fullPath))
                            {
                                if (verbose) WriteInfo($"Folder \"{fullPath}\" was excluded");
                                continue;
                            }
                            files = FsHelper.FindFiles(fullPath, recursive: !noRecursive, extensions: extensions).ToArray();
                            if (files.Length < 1)
                            {
                                if (verbose) WriteInfo($"Found no files in \"{fullPath}\"");
                                continue;
                            }
                            if (verbose) WriteInfo($"Found {files.Length} files in \"{fullPath}\"");
                            if (Trace)
                                foreach (string file in files)
                                    WriteTrace($"Going to process file \"{file}\"");
                            await worker.EnqueueRangeAsync(files).DynamicContext();
                            sources += files.Length;
                        }
                        else if (File.Exists(fullPath))
                        {
                            // Use a given filename
                            if (excluding.IsPathExcluded(fullPath))
                            {
                                if (verbose) WriteInfo($"File \"{fullPath}\" was excluded");
                                continue;
                            }
                            if (verbose) WriteInfo($"Add file \"{fullPath}\"");
                            await worker.EnqueueAsync(fullPath).DynamicContext();
                            sources++;
                        }
                        else
                        {
                            throw new FileNotFoundException("The given path wasn't found", fullPath);
                        }
                    }
                    // Wait until the parallel worker did finish all jobs
                    if (Trace) WriteTrace("Waiting for all files to finish processing");
                    await worker.WaitBoringAsync().DynamicContext();
                    if (worker.LastException is not null) throw new IOException("Failed to parse input sources", worker.LastException);
                }
            }
            if (verbose) WriteInfo($"Done processing input source files (found {keywords.Count} keywords in {sources} parsed source files; took {DateTime.Now - started})");
            // Write output
            started = DateTime.Now;
            POCatalog catalog;// Final PO catalog
            MemoryPoolStream? ms = null;// Memory stream for the PO generator
            Stream? outputStream = null;// Output (file?)stream
            try
            {
                if (mergeOutput && output is not null && File.Exists(output))
                {
                    // Merge to existing PO file
                    if (verbose) WriteInfo($"Merging results with existing PO file \"{output}\"");
                    if (keywords.Count < 1) throw new InvalidDataException("No keywords matched from input sources - won't touch the existing PO output file");
                    outputStream = FsHelper.CreateFileStream(output, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                    // Load existing PO contents
                    ms = new();
                    await outputStream.CopyToAsync(ms).DynamicContext();
                    ms.Position = 0;
                    POParseResult result = new POParser().Parse(ms);
                    ms.SetLength(0);
                    if (Trace || !result.Success)
                        foreach (Diagnostic diag in result.Diagnostics)
                            switch (diag.Severity)
                            {
                                case DiagnosticSeverity.Unknown:
                                    WriteDebug($"PO parser code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                case DiagnosticSeverity.Information:
                                    WriteInfo($"PO parser information code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                case DiagnosticSeverity.Warning:
                                    WriteWarning($"PO parser warning code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                case DiagnosticSeverity.Error:
                                    WriteError($"PO parser error code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                default:
                                    WriteWarning($"PO parser {diag.Severity} code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                            }
                    if (!result.Success)
                    {
                        if (System.Diagnostics.Debugger.IsAttached) System.Diagnostics.Debugger.Break();
                        throw new InvalidDataException($"Failed to read existing PO file \"{output}\" for merging the extraction results");
                    }
                    if (verbose && !Trace)
                        foreach (Diagnostic diag in result.Diagnostics.Where(d => d.Severity > DiagnosticSeverity.Unknown))
                            switch (diag.Severity)
                            {
                                case DiagnosticSeverity.Information:
                                    WriteInfo($"PO parser information code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                case DiagnosticSeverity.Warning:
                                    WriteWarning($"PO parser warning code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                case DiagnosticSeverity.Error:
                                    WriteError($"PO parser error code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                                default:
                                    WriteWarning($"PO parser {diag.Severity} code \"{diag.Code}\", arguments {diag.Args.Length}: {diag}");
                                    break;
                            }
                    if (result.Diagnostics.HasError) FailOnErrorIfRequested();
                    catalog = result.Catalog;
                    if (string.IsNullOrWhiteSpace(catalog.Encoding))
                    {
                        if (Trace) WriteTrace("Add missing encoding to PO output");
                        catalog.Encoding = Encoding.UTF8.WebName;
                    }
                    else if (Encoding.GetEncoding(catalog.Encoding) != Encoding.UTF8)
                    {
                        WriteWarning($"PO encoding was set to \"{catalog.Encoding}\", which might cause encoding problems");
                    }
                    // Merge catalog with our results
                    int newKeywords = 0,// Number of new keywords
                        existingKeywords = 0,// Number of updated keywords
                        fuzzyKeywords = 0,// Number of fuzzy logic updated keywords
                        minWeight = fuzzy > 0 ? 100 - fuzzy : 0;// Minimum weight for fuzzy keyword lookup
                    string[] catalogKeywords = [.. catalog.Keys.Select(k => k.Id)];// Existing catalog keywords
                    POReferenceComment referencesComment;// Keyword references comment
                    POFlagsComment fuzzyFlagComment = new()// Fuzzy logic updated keyword flag comment
                    {
                        Flags = new HashSet<string>()
                        {
                            "fuzzy"
                        }
                    };
                    foreach (ParserMatch match in keywords)
                    {
                        referencesComment = new()
                        {
                            References = new List<POSourceReference>(match.Positions.Select(p => new POSourceReference(p.FileName ?? "STDIN", p.LineNumber)))
                        };
                        if (catalog.TryGetValue(new(match.Keyword), out IPOEntry? entry))
                        {
                            // Update existing entry
                            if (Trace) WriteTrace($"Keyword \"{match.KeywordLiteral}\" found at {match.Positions.Count} position(s) exists already - updating references comment only");
                            if (entry.Comments is null)
                            {
                                if (Trace) WriteTrace("Creating comments");
                                entry.Comments = [ referencesComment ];
                            }
                            else
                            {
                                if (entry.Comments.FirstOrDefault(c => c is POReferenceComment) is POComment referenceComment)
                                {
                                    if (Trace) WriteTrace("Removing previous references");
                                    entry.Comments.Remove(referenceComment);
                                }
                                entry.Comments.Add(referencesComment);
                            }
                            existingKeywords++;
                            continue;
                        }
                        else if (fuzzy > 0 && catalogKeywords.Length > 0 && FuzzyKeywordLookup(match.Keyword, catalogKeywords, minWeight) is string fuzzyKeyword)
                        {
                            // Fuzzy keyword update
                            if (Trace) WriteTrace($"Keyword \"{match.KeywordLiteral}\" at {match.Positions.Count} position(s) exists already (found by fuzzy matching) - updating the entry \"{fuzzyKeyword.ToLiteral()}\"");
                            IPOEntry fuzzyEntry = catalog[new POKey(fuzzyKeyword)],
                                newEntry = fuzzyEntry is POSingularEntry singular
                                    ? new POSingularEntry(new(match.Keyword))
                                    {
                                        Comments = fuzzyEntry.Comments,
                                        Translation = singular.Translation
                                    }
                                    : new POPluralEntry(new(match.Keyword), fuzzyEntry)
                                    {
                                        Comments = fuzzyEntry.Comments,
                                    };
                            if (newEntry.Comments is null)
                            {
                                // Create comments
                                if (Trace) WriteTrace("Creating comments");
                                newEntry.Comments = [referencesComment, fuzzyFlagComment];
                            }
                            else if(newEntry.Comments.FirstOrDefault(c => c is POFlagsComment) is POFlagsComment flagsComment)
                            {
                                // Add fuzzy flag
                                if (!flagsComment.Flags.Contains("fuzzy"))
                                {
                                    if (Trace) WriteTrace("Ading fuzzy flag");
                                    flagsComment.Flags.Add("fuzzy");
                                }
                            }
                            else
                            {
                                // Add fuzzy flag comment
                                if (Trace) WriteTrace("Adding fuzzy flag comment");
                                newEntry.Comments.Add(fuzzyFlagComment);
                            }
                            if (newEntry.Comments.FirstOrDefault(c => c is POPreviousValueComment pvc && pvc.IdKind == POIdKind.Id) is POComment pvComment)
                            {
                                // Remove old previous value comment
                                if (Trace) WriteTrace("Removing old previous value comment");
                                newEntry.Comments.Remove(pvComment);
                            }
                            newEntry.Comments.Add(new POPreviousValueComment()
                            {
                                IdKind = POIdKind.Id,
                                Value = fuzzyEntry.Key.Id
                            });
                            // Exchange the entry
                            catalog.Remove(fuzzyEntry.Key);
                            catalog.Add(newEntry);
                            fuzzyKeywords++;
                            continue;
                        }
                        // Create new entry
                        if (Trace) WriteTrace($"Adding new keyword \"{match.KeywordLiteral}\" found at {match.Positions.Count} position(s)");
                        catalog.Add(new POSingularEntry(new(match.Keyword))
                        {
                            Comments = [ referencesComment ],
                            Translation = string.Empty
                        });
                        newKeywords++;
                    }
                    // Handle obsolete keywords
                    int obsolete = 0;// Number of removed obsolete keywords
                    foreach (IPOEntry entry in catalog.Values.Where(entry => !keywords.Any(kw => kw.Keyword == entry.Key.Id)).ToArray())
                    {
                        if (Trace) WriteTrace($"Removing obsolete keyword \"{entry.Key.Id.ToLiteral()}\"");
                        catalog.Remove(entry);
                        obsolete++;
                    }
                    if (verbose) WriteInfo($"Merging PO contents done ({newKeywords} keywords added, {existingKeywords} updated, {fuzzyKeywords} fuzzy updates, {obsolete} obsolete keywords removed)");
                    // Write final PO contents
                    if (Trace) WriteTrace($"Writing new PO contents to the existing PO output file \"{output}\"");
                    outputStream.SetLength(0);
                }
                else
                {
                    // Create new PO file or write to STDOUT
                    if (verbose) WriteInfo($"Writing the PO output to {(output is null ? "STDOUT" : $"\"{output}\"")}");
                    outputStream = output is null
                        ? Console.OpenStandardOutput()
                        : FsHelper.CreateFileStream(output, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
                    // Entries
                    catalog = new(keywords.Select(k => new POSingularEntry(new(k.Keyword))
                    {
                        Comments = [
                            new POReferenceComment()
                            {
                                References = new List<POSourceReference>(k.Positions.Select(p => new POSourceReference(p.FileName ?? "STDIN", p.LineNumber)))
                            }
                        ],
                        Translation = string.Empty
                    }))
                    {
                        Encoding = Encoding.UTF8.WebName
                    };
                    // Header
                    if (!noHeader)
                    {
                        if (verbose) WriteInfo("Adding PO header");
                        catalog.HeaderComments = [
                            new POTranslatorComment()
                            {
                                Text = "wan24PoeditParser"
                            }
                        ];
                        catalog.Headers = new Dictionary<string, string>()
                        {
                            { "Project-Id-Version", $"wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}" },
                            { "Report-Msgid-Bugs-To", "https://github.com/nd1012/wan24-PoeditParser/issues" },
                            { "MIME-Version", "1.0" },
                            { "Content-Type", "text/plain; charset=UTF-8" },
                            { "Content-Transfer-Encoding", "8bit" },
                            { "X-Generator", $"wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}" },
                            { "X-Poedit-SourceCharset", ParserConfig.SourceEncoding.WebName },
                        };
                    }
                    // Save the PO contents
                    if (Trace) WriteTrace($"Writing PO contents to {(output is null ? "STDOUT" : $"the output PO file \"{output}\"")}");
                }
                // Generate PO
                ms ??= new();
                using (TextWriter writer = new StreamWriter(ms, Encoding.UTF8, leaveOpen: true))
                    new POGenerator().Generate(writer, catalog);
                ms.Position = 0;
                await ms.CopyToAsync(outputStream).DynamicContext();
                if (verbose) WriteInfo($"Done writing the PO output with {catalog.Count} entries (took {DateTime.Now - started}; total runtime {DateTime.Now - start})");
            }
            finally
            {
                ms?.Dispose();
                if (outputStream is not null) await outputStream.DisposeAsync().DynamicContext();
            }
        }
    }
}
