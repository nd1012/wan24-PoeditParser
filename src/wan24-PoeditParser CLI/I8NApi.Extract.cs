﻿using Karambolo.PO;
using System.ComponentModel;
using System.Reflection;
using wan24.CLI;
using wan24.Core;
using static wan24.Core.Logger;
using static wan24.Core.Logging;

namespace wan24.PoeditParser
{
    // Extract
    public sealed partial class I8NApi
    {
        /// <summary>
        /// Extract an internationalization file
        /// </summary>
        /// <param name="input">Internationalization input filename (if not given, STDIN will be used)</param>
        /// <param name="uncompress">To uncompress the internationalization file (not required to use, if a header will be red)</param>
        /// <param name="jsonOutput">JSON (UTF-8) output filename</param>
        /// <param name="poOutput">PO (gettext) output filename</param>
        /// <param name="poeditPluralHeader">Poedit plural header value</param>
        /// <param name="json">To write JSON (UTF-8) to STDOUT</param>
        /// <param name="po">To write PO (gettext) (UTF-8) to STDOUT</param>
        /// <param name="noHeader">To skip reading a header with the version number and the compression flag</param>
        /// <param name="verbose">Write verbose informations to STDERR</param>
        [CliApi("extract")]
        [DisplayText("Extract i8n file")]
        [Description("Extract an internationalization (i8n) file to JSON or PO (gettext) format")]
        [StdIn("/path/to/input.i8n")]
        [StdOut("/path/to/output.(json|po)")]
        public static async Task ExtractAsync(

            [CliApi(Example = "/path/to/input.i8n")]
            [DisplayText("Input")]
            [Description("Internationalization input filename (if not given, STDIN will be used)")]
            string? input = null,

            [CliApi]
            [DisplayText("Uncompress")]
            [Description("To uncompress the internationalization file (not required to use, if a header will be red)")]
            bool uncompress = false,

            [CliApi(Example = "/path/to/output.json")]
            [DisplayText("JSON output")]
            [Description("JSON (UTF-8) output filename")]
            string? jsonOutput = null,

            [CliApi(Example = "/path/to/output.po")]
            [DisplayText("PO output")]
            [Description("PO (gettext) (UTF-8) output filename")]
            string? poOutput = null,

            [CliApi(Example = "\"nplurals=2; plural=(n != 1);\"")]
            [DisplayText("Poedit plural header")]
            [Description("Poedit plural header value (see example) - default is: \"nplurals=2; plural=(n != 1);\"")]
            string poeditPluralHeader = "nplurals=2; plural=(n != 1);",

            [CliApi]
            [DisplayText("JSON")]
            [Description("To write JSON (UTF-8) to STDOUT")]
            bool json = false,

            [CliApi]
            [DisplayText("PO")]
            [Description("To write PO (gettext) (UTF-8) to STDOUT")]
            bool po = false,

            [CliApi]
            [DisplayText("No header")]
            [Description("To skip reading a header with the version number and the compression flag")]
            bool noHeader = false,

            [CliApi]
            [DisplayText("Verbose")]
            [Description("Write verbose informations to STDERR")]
            bool verbose = false

            )
        {
            verbose |= Trace;
            if (Trace) WriteTrace("Extracting internationalization file");
            if (json && po) throw new ArgumentException("Can't write JSON AND PO to STDOUT", nameof(po));
            if (!json && !po && jsonOutput is null && poOutput is null) json = true;
            // Read internationalization input
            Stream inputStream = input is null
                ? Console.OpenStandardInput()
                : FsHelper.CreateFileStream(input, FileMode.Open, FileAccess.Read, FileShare.Read);
            (_, _, Dictionary<string, string[]> terms) = await ReadI8NAsync(inputStream, noHeader, uncompress, verbose).DynamicContext();
            // Write JSON output file
            if (jsonOutput is not null)
            {
                if (verbose) WriteInfo($"Writing JSON to ouput file \"{jsonOutput}\"");
                FileStream fs = FsHelper.CreateFileStream(jsonOutput, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
                await using (fs.DynamicContext())
                    await JsonHelper.EncodeAsync(terms, fs, prettify: true).DynamicContext();
            }
            // Write JSON to STDOUT
            if (json)
            {
                if (verbose) WriteInfo($"Writing JSON to STDOUT");
                Stream stdout = Console.OpenStandardOutput();
                await using (stdout.DynamicContext())
                    await JsonHelper.EncodeAsync(terms, stdout, prettify: true).DynamicContext();
            }
            // Generate PO in memory
            if (poOutput is null && !po)
            {
                if (verbose) WriteInfo("Done extracting internationalization file");
                return;
            }
            using MemoryPoolStream ms = new();
            new POGenerator().Generate(ms, new(terms.Select(kvp => (IPOEntry)(kvp.Value.Length > 0
                ? new POPluralEntry(new(kvp.Key), kvp.Value)
                : new POSingularEntry(new(kvp.Key))
                {
                    Translation = kvp.Value.Length > 0 ? kvp.Value[0] : string.Empty
                })))
            {
                HeaderComments = [
                    new POTranslatorComment()
                    {
                        Text = "wan24PoeditParser i8n"
                    }
                ],
                Headers = new Dictionary<string, string>()
                {
                    { "Project-Id-Version", $"wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}" },
                    { "Report-Msgid-Bugs-To", "https://github.com/nd1012/wan24-PoeditParser/issues" },
                    { "MIME-Version", "1.0" },
                    { "Content-Type", "text/plain; charset=UTF-8" },
                    { "Content-Transfer-Encoding", "8bit" },
                    { "X-Generator", $"wan24PoeditParser {Assembly.GetExecutingAssembly().GetCustomAttributeCached<AssemblyInformationalVersionAttribute>()?.InformationalVersion}" },
                    { "X-Poedit-SourceCharset", ParserConfig.SourceEncoding.WebName },
                    { "Plural-Forms", poeditPluralHeader }
                }
            });
            // Write PO output file
            if (poOutput is not null)
            {
                if (verbose) WriteInfo($"Writing PO to ouput file \"{jsonOutput}\"");
                ms.Position = 0;
                FileStream fs = FsHelper.CreateFileStream(poOutput, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, overwrite: true);
                await using (fs.DynamicContext())
                    await ms.CopyToAsync(fs).DynamicContext();
            }
            // Write PO to STDOUT
            if (po)
            {
                if (verbose) WriteInfo($"Writing PO to STDOUT");
                ms.Position = 0;
                Stream stdout = Console.OpenStandardOutput();
                await using (stdout.DynamicContext())
                    await ms.CopyToAsync(stdout).DynamicContext();
            }
            if (verbose) WriteInfo("Done extracting internationalization file");
        }
    }
}