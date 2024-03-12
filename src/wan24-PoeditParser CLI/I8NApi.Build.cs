using GetText.Loaders;
using Karambolo.PO;
using System.ComponentModel;
using wan24.CLI;
using wan24.Compression;
using wan24.Core;
using wan24.StreamSerializerExtensions;
using static wan24.Core.Logger;
using static wan24.Core.Logging;

namespace wan24.PoeditParser
{
    // Build
    public sealed partial class I8NApi
    {
        /// <summary>
        /// Build an internationalization file from multiple input sources
        /// </summary>
        /// <param name="jsonInput">JSON (UTF-8) input filenames</param>
        /// <param name="poInput">PO (gettext) input filenames</param>
        /// <param name="moInput">MO (gettext) input filenames</param>
        /// <param name="output">Internationalization output filename (if not given, STDOUT will be used; existing file will be overwritten)</param>
        /// <param name="compress">To compress the internationalization file</param>
        /// <param name="json">To read JSON (UTF-8) from STDIN</param>
        /// <param name="po">To read PO (gettext) from STDIN</param>
        /// <param name="mo">To read MO (gettext) from STDIN</param>
        /// <param name="noHeader">To skip writing a header with the version number and the compression flag</param>
        /// <param name="verbose">Write verbose informations to STDERR</param>
        /// <param name="failOnExistingKey">To fail, if an existing key would be overwritten by an additional source</param>
        [CliApi("build", IsDefault = true)]
        [DisplayText("Build i8n file")]
        [Description("Build an internationalization (i8n) file from JSON (UTF-8) and/or PO/MO (gettext) source files")]
        [StdIn("/path/to/input.(json|po|mo)")]
        [StdOut("/path/to/output.i8n")]
        public static async Task BuildAsync(

            [CliApi(Example = "/path/to/input.json")]
            [DisplayText("JSON input")]
            [Description("JSON (UTF-8) input filenames")]
            string[]? jsonInput = null,

            [CliApi(Example = "/path/to/input.po")]
            [DisplayText("PO input")]
            [Description("PO (gettext) input filenames")]
            string[]? poInput = null,

            [CliApi(Example = "/path/to/input.mo")]
            [DisplayText("MO input")]
            [Description("MO (gettext) input filenames")]
            string[]? moInput = null,

            [CliApi(Example = "/path/to/output.i8n")]
            [DisplayText("Output")]
            [Description("Internationalization output filename (if not given, STDOUT will be used; existing file will be overwritten)")]
            string? output = null,

            [CliApi]
            [DisplayText("Compress")]
            [Description("To compress the internationalization file")]
            bool compress = false,

            [CliApi]
            [DisplayText("JSON")]
            [Description("To read JSON (UTF-8) from STDIN")]
            bool json = false,

            [CliApi]
            [DisplayText("PO")]
            [Description("To read PO (gettext) from STDIN")]
            bool po = false,

            [CliApi]
            [DisplayText("MO")]
            [Description("To read MO (gettext) from STDIN")]
            bool mo = false,

            [CliApi]
            [DisplayText("No header")]
            [Description("To skip writing a header with the version number and the compression flag")]
            bool noHeader = false,

            [CliApi]
            [DisplayText("Verbose")]
            [Description("Write verbose informations to STDERR")]
            bool verbose = false,

            [CliApi]
            [DisplayText("Fail on existing key")]
            [Description("To fail, if an existing key would be overwritten by an additional source")]
            bool failOnExistingKey = false

            )
        {
            verbose |= Trace;
            if (Trace) WriteTrace("Creating internationalization file");
            int stdInCnt = 0;
            if (json) stdInCnt++;
            if (po) stdInCnt++;
            if (mo) stdInCnt++;
            if (stdInCnt > 1) throw new InvalidOperationException("Can't parse multiple input formats from STDIN");
            Dictionary<string, string[]> terms = [];
            // Read JSON source files
            if (jsonInput is not null && jsonInput.Length > 0)
                foreach (string fn in jsonInput)
                {
                    if (verbose) WriteInfo($"Processing JSON source file \"{fn}\"");
                    await ReadJsonSourceAsync(FsHelper.CreateFileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read), fn, terms, failOnExistingKey, verbose).DynamicContext();
                }
            // Read MO source files
            MoFileParser? moParser = null;
            if (moInput is not null && moInput.Length > 0)
            {
                moParser = new();
                using MemoryPoolStream ms = new();
                foreach (string fn in moInput)
                {
                    if (verbose) WriteInfo($"Processing MO source file \"{fn}\"");
                    await ReadMoSourceAsync(FsHelper.CreateFileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read), fn, moParser, ms, terms, failOnExistingKey, verbose).DynamicContext();
                }
            }
            // Read PO source files
            POParser? poParser = null;
            if (poInput is not null && poInput.Length > 0)
            {
                poParser = new();
                using MemoryPoolStream ms = new();
                foreach (string fn in poInput)
                {
                    if (verbose) WriteInfo($"Processing PO source file \"{fn}\"");
                    await ReadPoSourceAsync(FsHelper.CreateFileStream(fn, FileMode.Open, FileAccess.Read, FileShare.Read), fn, poParser, ms, terms, failOnExistingKey, verbose).DynamicContext();
                }
            }
            // Read JSON from STDIN
            if (json)
            {
                if (verbose) WriteInfo("Processing JSON from STDIN");
                await ReadJsonSourceAsync(Console.OpenStandardInput(), fn: null, terms, failOnExistingKey, verbose).DynamicContext();
            }
            // Read MO from STDIN
            if (mo)
            {
                if (verbose) WriteInfo("Processing MO from STDIN");
                moParser ??= new();
                using MemoryPoolStream ms = new();
                await ReadMoSourceAsync(Console.OpenStandardInput(), fn: null, moParser, ms, terms, failOnExistingKey, verbose).DynamicContext();
            }
            // Read PO from STDIN
            if (po)
            {
                if (verbose) WriteInfo("Processing PO from STDIN");
                poParser ??= new();
                using MemoryPoolStream ms = new();
                await ReadPoSourceAsync(Console.OpenStandardInput(), fn: null, poParser, ms, terms, failOnExistingKey, verbose).DynamicContext();
            }
            if (verbose) WriteInfo($"Found {terms.Count} terms in total");
            // Write output
            if (verbose) WriteInfo($"Writing internationalization file to {(output is null ? "STDOUT" : $"output file \"{output}\"")}");
            Stream outputStream = output is null
                ? Console.OpenStandardOutput()
                : FsHelper.CreateFileStream(output, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
            await using (outputStream.DynamicContext())
            {
                // Header
                if (!noHeader)
                {
                    if (Trace) WriteTrace("Writing header");
                    int header = VERSION;
                    if (compress) header |= 128;
                    if (Trace) WriteTrace($"Writing header {header} (version {VERSION}, compressed: {compress})");
                    await outputStream.WriteAsync((byte)header).DynamicContext();
                }
                // Body
                if (compress)
                {
                    // Compressed
                    if (Trace) WriteTrace($"Use compression \"{CompressionHelper.DefaultAlgorithm.DisplayName}\"");
                    using MemoryPoolStream ms = new();
                    await JsonHelper.EncodeAsync(terms, ms).DynamicContext();
                    ms.Position = 0;
                    CompressionOptions options = CompressionHelper.DefaultAlgorithm.DefaultOptions;
                    options.FlagsIncluded = true;
                    options.AlgorithmIncluded = true;
                    options.UncompressedLengthIncluded = true;
                    options.LeaveOpen = true;
                    options = await CompressionHelper.DefaultAlgorithm.WriteOptionsAsync(ms, outputStream, options).DynamicContext();
                    using Stream compression = CompressionHelper.DefaultAlgorithm.GetCompressionStream(outputStream, options);
                    await ms.CopyToAsync(compression).DynamicContext();
                }
                else
                {
                    // Uncompressed
                    if (Trace) WriteTrace("Write uncompressed");
                    await JsonHelper.EncodeAsync(terms, outputStream).DynamicContext();
                }
            }
            if (verbose) WriteInfo("Done writing internationalization output");
        }

        /// <summary>
        /// Build many internationalization (i8n) files from JSON (UTF-8) and/or PO/MO (gettext) source files (output filename is the input filename with the 
        /// <c>.i8n</c> extension instead - existing files will be overwritten; default is to convert all *.json/po/mo files in the working folder)
        /// </summary>
        /// <param name="jsonInput">JSON input file (UTF-8) folder (no recursion)</param>
        /// <param name="jsonInputPattern">JSON input pattern</param>
        /// <param name="poInput">PO (gettext) input file folder (no recursion)</param>
        /// <param name="poInputPattern">PO input pattern</param>
        /// <param name="moInput">MO (gettext) input file folder (no recursion)</param>
        /// <param name="moInputPattern">MO input pattern</param>
        /// <param name="compress">To compress the internationalization files</param>
        /// <param name="noHeader">To skip writing a header with the version number and the compression flag</param>
        /// <param name="verbose">Write verbose informations to STDERR</param>
        [CliApi("buildmany", IsDefault = true)]
        [DisplayText("Build i8n files")]
        [Description("Build many internationalization (i8n) files from JSON (UTF-8) or PO/MO (gettext) source files (output filename is the input filename with the \".i8n\" extension instead - existing files will be overwritten; default is to convert all *.json/po/mo files in the working folder)")]
        public static async Task BuildManyAsync(

            [CliApi(Example = "/path/to/sources")]
            [DisplayText("JSON input")]
            [Description("JSON input file (UTF-8) folder (no recursion; default is the working folder)")]
            string jsonInput = "./",

            [CliApi(Example = "*.json")]
            [DisplayText("JSON input pattern")]
            [Description("JSON input pattern (default is \"*.json\")")]
            string jsonInputPattern = "*.json",

            [CliApi(Example = "/path/to/sources")]
            [DisplayText("PO input")]
            [Description("PO (gettext) input file folder (no recursion; default is the working folder)")]
            string poInput = "./",

            [CliApi(Example = "*.po")]
            [DisplayText("PO input pattern")]
            [Description("PO (gettext) input pattern (default is \"*.po\")")]
            string poInputPattern = "*.po",

            [CliApi(Example = "/path/to/sources")]
            [DisplayText("MO input")]
            [Description("MO (gettext) input file folder (no recursion; default is the working folder)")]
            string moInput = "./",

            [CliApi(Example = "*.mo")]
            [DisplayText("MO input pattern")]
            [Description("MO input pattern (default is \"*.mo\")")]
            string moInputPattern = "*.mo",

            [CliApi]
            [DisplayText("Compress")]
            [Description("To compress the internationalization files")]
            bool compress = false,

            [CliApi]
            [DisplayText("No header")]
            [Description("To skip writing a header with the version number and the compression flag")]
            bool noHeader = false,

            [CliApi]
            [DisplayText("Verbose")]
            [Description("Write verbose informations to STDERR")]
            bool verbose = false

            )
        {
            verbose |= Trace;
            if (Trace) WriteTrace("Creating many internationalization files");
            foreach (string fn in FsHelper.FindFiles(jsonInput, searchPattern: jsonInputPattern, recursive: false))
                await BuildAsync(
                    jsonInput: [fn], 
                    output: Path.Combine(jsonInput, $"{Path.GetFileNameWithoutExtension(fn)}.i8n"), 
                    compress: compress, 
                    noHeader: noHeader, 
                    verbose: verbose
                    ).DynamicContext();
            foreach (string fn in FsHelper.FindFiles(poInput, searchPattern: poInputPattern, recursive: false))
                await BuildAsync(
                    poInput: [fn],
                    output: Path.Combine(poInput, $"{Path.GetFileNameWithoutExtension(fn)}.i8n"),
                    compress: compress,
                    noHeader: noHeader,
                    verbose: verbose
                    ).DynamicContext();
            foreach (string fn in FsHelper.FindFiles(moInput, searchPattern: moInputPattern, recursive: false))
                await BuildAsync(
                    moInput: [fn],
                    output: Path.Combine(moInput, $"{Path.GetFileNameWithoutExtension(fn)}.i8n"),
                    compress: compress,
                    noHeader: noHeader,
                    verbose: verbose
                    ).DynamicContext();
            if (Trace) WriteTrace("Done creating many internationalization files");
        }
    }
}
