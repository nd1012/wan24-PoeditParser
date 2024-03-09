using System.Text.RegularExpressions;
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
        /// Applied Poedit app configuration
        /// </summary>
        public static ParserAppConfig? AppliedPoeditConfig { get; private set; }

        /// <summary>
        /// Core app configuration
        /// </summary>
        public AppConfig? Core { get; set; }

        /// <summary>
        /// Use only a single thread?
        /// </summary>
        public bool SingleThread { get; set; }

        /// <summary>
        /// Source text encoding identifier
        /// </summary>
        public string? Encoding { get; set; }

        /// <summary>
        /// Custom search(/replace) regular expression patterns
        /// </summary>
        public string[][]? Patterns { get; set; }

        /// <summary>
        /// File extensions to look for
        /// </summary>
        public string[]? FileExtensions { get; set; }

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
        }
    }
}
