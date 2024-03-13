using System.Text;
using System.Text.RegularExpressions;

namespace wan24.PoeditParser
{
    /// <summary>
    /// Poedit parser configuration
    /// </summary>
    public static class ParserConfig
    {
        /// <summary>
        /// Constructor
        /// </summary>
        static ParserConfig()
        {
            Patterns = [
                // Attributes
                new ParserPattern()
                {
                    Pattern = @"(Description|DisplayText)\(\s*\"".*[^\\]\""\s*\)",
                    Options = RegexOptions.None
                },
                new ParserPattern()
                {
                    Pattern = @"^.*(Description|DisplayText)\(\s*(\"".*[^\\]\"")\s*\).*$",
                    Options = RegexOptions.None,
                    Replacement = "$2"
                },
                // Translation methods
                new ParserPattern()
                {
                    Pattern = @"(__?|gettextn?|Translate(Plural)?|GetTerm|StdIn|StdOut)\(\s*\"".*[^\\]\""",
                    Options = RegexOptions.None
                },
                new ParserPattern()
                {
                    Pattern = @"^.*(__?|gettextn?|Translate(Plural)?|GetTerm|StdIn|StdOut)\(\s*(\"".*[^\\]\"").*$",
                    Options = RegexOptions.None,
                    Replacement = "$3"
                },
                // CliApi attribute examples
                new ParserPattern()
                {
                    Pattern = @"CliApi[^\(]*\([^\)]*Example\s*\=\s*\"".*[^\\]\""",
                    Options = RegexOptions.None
                },
                new ParserPattern()
                {
                    Pattern = @"^.*CliApi[^\(]*\([^\)]*Example\s*\=\s*(\"".*[^\\]\"").*$",
                    Options = RegexOptions.None,
                    Replacement = "$1"
                },
                // ExitCode attribute examples
                new ParserPattern()
                {
                    Pattern = @"ExitCode[^\(]*\(\d+,\s*\"".*[^\\]\""",
                    Options = RegexOptions.None
                },
                new ParserPattern()
                {
                    Pattern = @"^.*ExitCode[^\(]*\(\d+,\s*(\"".*[^\\]\"").*$",
                    Options = RegexOptions.None,
                    Replacement = "$1"
                },
                // Forced strings
                new ParserPattern()
                {
                    Pattern = @"[^\@\$]\"".*[^\\]\"".*;.*\/\/.*wan24PoeditParser\:include",
                    Options = RegexOptions.IgnoreCase
                },
                new ParserPattern()
                {
                    Pattern = @"^.*[^\@\$](\"".*[^\\]\"").*;.*\/\/.*wan24PoeditParser\:include.*$",
                    Options = RegexOptions.IgnoreCase,
                    Replacement = "$1"
                },
                // Cut the tail of multiple possible keywords within one line to get only one, finally
                new ParserPattern()
                {
                    Pattern = @"^\s*(\"".*[^\\]\"").+$",
                    Options = RegexOptions.None,
                    Replacement = "$1"
                }
                ];
            FileExtensions = [".cs", ".razor", ".cshtml", ".aspx", ".cake", ".vb"];
            SourceEncoding = Encoding.UTF8;
        }

        /// <summary>
        /// Use only a single thread?
        /// </summary>
        public static bool SingleThread { get; set; }

        /// <summary>
        /// Source text encoding
        /// </summary>
        public static Encoding SourceEncoding { get; set; }

        /// <summary>
        /// Parser patterns
        /// </summary>
        public static HashSet<ParserPattern> Patterns { get; }

        /// <summary>
        /// File extensions to look for
        /// </summary>
        public static HashSet<string> FileExtensions { get; }

        /// <summary>
        /// Merge the PO contents with an existing output PO file?
        /// </summary>
        public static bool MergeOutput { get; set; }

        /// <summary>
        /// Fail the whole process on any error?
        /// </summary>
        public static bool FailOnError { get; set; }
    }
}
