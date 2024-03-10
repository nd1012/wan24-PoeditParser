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
                new ParserPattern()
                {
                    Pattern= @"^.*((Description|DisplayText)\(\s*(\"".*[^\\]\"")\s*\)).*$",
                    Options = RegexOptions.Compiled,
                    Replacement = "$3"
                },
                new ParserPattern()
                {
                    Pattern= @"^.*((__?|gettextn?|Translate(Plural)?|GetTerm)\(\s*(\"".*[^\\]\"")).*$",
                    Options = RegexOptions.Compiled,
                    Replacement = "$4"
                },
                new ParserPattern()
                {
                    Pattern= @"^.*(CliApi[^\s]*\([^\)]*Example\s*\=\s*(\"".*[^\\]\"")).*$",
                    Options = RegexOptions.Compiled,
                    Replacement = "$2"
                }
                ];
            FileExtensions = [".cs"];
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
        public static List<ParserPattern> Patterns { get; }

        /// <summary>
        /// File extensions to look for
        /// </summary>
        public static HashSet<string> FileExtensions { get; }
    }
}
