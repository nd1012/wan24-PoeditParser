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
    /// Poedit parser CLI API
    /// </summary>
    [CliApi("parser", IsDefault = true)]
    [DisplayText("Poedit parser")]
    [Description("CLI API for parsing a PO file from source code")]
    public sealed partial class ParserApi
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
            if (config is not null)
            {
                // Load custom JSON configuration file
                if (Trace) WriteTrace($"Loading JSON configuration from \"{config}\"");
                await AppConfig.LoadAsync<ParserAppConfig>(config).DynamicContext();
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
            if (encoding is not null)
            {
                // Override source encoding
                if (Trace) WriteTrace($"Override source encoding with \"{encoding}\"");
                ParserConfig.SourceEncoding = Encoding.GetEncoding(encoding);
            }
            if (verbose && Logging.Logger is null) Logging.Logger = new VividConsoleLogger(LogLevel.Information);// Ensure having a logger for verbose output
            if (!verbose)
            {
                // Always be verbose if a logger was configured
                if (Trace) WriteTrace($"Force verbose {Logging.Logger is not null && Info}");
                verbose = Logging.Logger is not null && Info;
            }
            if (verbose)
            {
                // Output the used final settings
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
                FileWorker worker = new(ParserConfig.SingleThread ? 1 : Environment.ProcessorCount << 1, keywords, verbose)
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
                        fullPath = Path.GetFullPath(path);
                        if (Directory.Exists(fullPath))
                        {
                            // Find files in a folder (optional recursive)
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
                            if (Trace)
                                foreach (string file in files)
                                    WriteTrace($"Going to process file \"{file}\"");
                            await worker.EnqueueRangeAsync(files).DynamicContext();
                        }
                        else if (File.Exists(fullPath))
                        {
                            // Use a given filename
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
                    // Wait until the parallel worker did finish all jobs
                    if (Trace) WriteTrace("Waiting for all files to finish processing");
                    await worker.WaitBoringAsync().DynamicContext();
                }
            }
            if (verbose) WriteInfo($"Done processing input source files (found {keywords.Count} keywords)");
            // Write output
            if (verbose) WriteInfo($"Writing the PO output to {(output is null ? "STDOUT" : $"\"{output}\"")}");
            Stream outputStream = output is null
                ? Console.OpenStandardOutput()
                : FsHelper.CreateFileStream(output, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
            await using (outputStream.DynamicContext())
            {
                using StreamWriter writer = new(outputStream, Encoding.UTF8);// PO files use UTF-8 always
                // Header
                if (!noHeader)
                {
                    if (verbose) WriteInfo("Writing PO header");
                    await writer.WriteLineAsync($"# PO file created using wan24PoeditParser").DynamicContext();
                    await writer.WriteLineAsync($"# https://github.com/nd1012/wan24-PoeditParser").DynamicContext();
                    await writer.WriteLineAsync($"#").DynamicContext();
                    await writer.WriteLineAsync($"msgid \"\"").DynamicContext();
                    await writer.WriteLineAsync($"msgstr \"\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{$"Project-Id-Version: wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"Report-Msgid-Bugs-To: https://github.com/nd1012/wan24-PoeditParser/issues\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"MIME-Version: 1.0\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"Content-Type: text/plain; charset=UTF-8\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{"Content-Transfer-Encoding: 8bit\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{$"X-Generator: wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                    await writer.WriteLineAsync($"\"{$"X-Poedit-SourceCharset: {ParserConfig.SourceEncoding.WebName}\n".ToPoeditMessageLiteral()}\"").DynamicContext();
                }
                // Found keywords
                foreach (ParserMatch match in keywords)
                {
                    if (verbose) WriteInfo($"Writing keyword \"{match.Keyword.ToPoeditMessageLiteral()}\"");
                    await WriteEntryAsync(writer, match).DynamicContext();
                }
            }
            if (verbose) WriteInfo("Done writing the PO output");
        }
    }
}
