using System.Text.RegularExpressions;
using wan24.CLI;
using wan24.Core;

namespace wan24.PoeditParser
{
    /// <summary>
    /// Poedit parser app configuration
    /// </summary>
    public sealed class ParserAppConfig : AppConfigBase
    {
        /// <summary>
        /// Constructor
        /// </summary>
        public ParserAppConfig() : base() { }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="setApplied">Set as the applied configuration?</param>
        public ParserAppConfig(in bool setApplied) : base() => SetApplied = setApplied;

        /// <summary>
        /// Applied Poedit app configuration
        /// </summary>
        public static ParserAppConfig? AppliedPoeditConfig { get; private set; }

        /// <summary>
        /// Core app configuration
        /// </summary>
        public AppConfig? Core { get; set; }

        /// <summary>
        /// CLI app configuration
        /// </summary>
        public CliAppConfig? CLI { get; set; }

        /// <summary>
        /// <see langword="true"/> to disable multi-threading (process only one source file per time)
        /// </summary>
        public bool SingleThread { get; set; }

        /// <summary>
        /// Text encoding of the source files (may be any encoding (web) identifier)
        /// </summary>
        public string? Encoding { get; set; }

        /// <summary>
        /// Custom search(/replace) regular expression patterns
        /// </summary>
        public string[][]? Patterns { get; set; }

        /// <summary>
        /// File extensions to look for (including dot)
        /// </summary>
        public string[]? FileExtensions { get; set; }

        /// <summary>
        /// Merge the PO contents with an existing output PO file?
        /// </summary>
        public bool MergeOutput { get; set; }

        /// <summary>
        /// Fail the whole process on any error?
        /// </summary>
        public bool FailOnError { get; set; }

        /// <summary>
        /// Merge this configuration with the default configuration?
        /// </summary>
        public bool Merge { get; set; }

        /// <inheritdoc/>
        public override void Apply()
        {
            if (SetApplied)
            {
                if (AppliedPoeditConfig is not null) throw new InvalidOperationException();
                AppliedPoeditConfig = this;
            }
            Core?.Apply();
            CLI?.Apply();
            ParserConfig.SingleThread = SingleThread;
            if (Encoding is not null) ParserConfig.SourceEncoding = System.Text.Encoding.GetEncoding(Encoding);
            if (Patterns is not null)
            {
                if (!Merge) ParserConfig.Patterns.Clear();
                foreach (string[] pattern in Patterns)
                {
                    if (pattern.Length != 2 && pattern.Length != 3)
                        throw new InvalidDataException($"Invalid pattern definition with {pattern.Length} elements");
                    ParserConfig.Patterns.Add(new ParserPattern()
                    {
                        Pattern = pattern[0],
                        Options = JsonHelper.Decode<RegexOptions>(pattern[1]),
                        Replacement = pattern.Length > 2 ? pattern[2] : null
                    });
                }
            }
            if (FileExtensions is not null)
            {
                if (!Merge) ParserConfig.FileExtensions.Clear();
                ParserConfig.FileExtensions.AddRange(FileExtensions);
            }
            ParserConfig.MergeOutput = MergeOutput;
            ParserConfig.FailOnError = FailOnError;
        }

        /// <inheritdoc/>
        public override async Task ApplyAsync(CancellationToken cancellationToken = default)
        {
            if (SetApplied)
            {
                if (AppliedPoeditConfig is not null) throw new InvalidOperationException();
                AppliedPoeditConfig = this;
            }
            if (Core is not null) await Core.ApplyAsync(cancellationToken).DynamicContext();
            if (CLI is not null) await CLI.ApplyAsync(cancellationToken).DynamicContext();
            ParserConfig.SingleThread = SingleThread;
            if (Encoding is not null) ParserConfig.SourceEncoding = System.Text.Encoding.GetEncoding(Encoding);
            if (Patterns is not null)
            {
                if (!Merge) ParserConfig.Patterns.Clear();
                foreach (string[] pattern in Patterns)
                {
                    if (pattern.Length != 2 && pattern.Length != 3)
                        throw new InvalidDataException($"Invalid pattern definition with {pattern.Length} elements");
                    ParserConfig.Patterns.Add(new ParserPattern()
                    {
                        Pattern = pattern[0],
                        Options = JsonHelper.Decode<RegexOptions>(pattern[1]),
                        Replacement = pattern.Length > 2 ? pattern[2] : null
                    });
                }
            }
            if (FileExtensions is not null)
            {
                if (!Merge) ParserConfig.FileExtensions.Clear();
                ParserConfig.FileExtensions.AddRange(FileExtensions);
            }
            ParserConfig.MergeOutput = MergeOutput;
            ParserConfig.FailOnError = FailOnError;
        }
    }
}
