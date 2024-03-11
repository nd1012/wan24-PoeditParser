# wan24-PoeditParser

This is a small dotnet tool for parsing source code for gettext strings and 
writing the result in the PO format to a file or STDOUT.

**CAUTION**: It can create a PO file from the command line, but using it as 
Poedit extractor didn't work yet (Poedit discards the custom extractor 
configuration, which may be a bug - not sure yet).

It's pre-configured for use with the 
[`wan24-Core`](https://github.com/WAN-Solutions/wan24-Core) translation 
helpers for C#, but it can be customized easily for any environment and any 
programming language by customizing the used regular expressions in your own 
configuration file.

## Usage

### Where to get it

The Poedit parser is available as a dotnet tool and can be installed from the 
command line:

```bash
dotnet tool install -g wan24-PoeditParser
```

The default installation folder is 

- `%USER%\.dotnet\tools` for Windows
- `~/.dotnet/tools` for Linux (or MAC)

**NOTE**: Please ensure that your global .NET tool path is in the `PATH` 
environment variable (open a new Windows terminal after adding the path using 
_Settings_ -> _System_ -> _Extended system settings_ -> _Extended_ -> 
_Environment variables_).

### Default file extensions

Per default these file extensions will be looked up when walking through a 
folder tree:

- `.cs`
- `.razor`
- `.cshtml`
- `.aspx`
- `.cake`
- `.vb`

These extensions try to match all possible .NET C#/VB source code files.

In the `config` folder you'll find configurations for other languages, while 
the `dotnet.json` contains the `wan24-PoeditParser` default configuration, 
which you may use as a template for customizations.

### Default keyword search

Per default keywords will be found by these regular expressions:

- `(Description|DisplayText)\(\s*(\".*[^\\]\")\s*\)`
- `(__?|gettextn?|Translate(Plural)?|GetTerm)\(\s*(\".*[^\\]\")`
- `CliApi[^\s]*\([^\)]*Example\s*\=\s*(\".*[^\\]\")`
- `[^\@\$]\".*[^\\]\".*;.*\/\/.*wan24PoeditParser\:include` (case insensitive)

They'll then be post-processed by these replacing regular expressions:

- `^.*(Description|DisplayText)\(\s*(\".*[^\\]\")\s*\).*$` -> `$2`
- `^.*(__?|gettextn?|Translate(Plural)?|GetTerm)\(\s*(\".*[^\\]\").*$` -> `$3`
- `^.*CliApi[^\s]*\([^\)]*Example\s*\=\s*(\".*[^\\]\").*$` -> `$1`
- `^.*[^\@\$](\".*[^\\]\").*;.*\/\/.*wan24PoeditParser\:include.*$` -> `$1` 
(case insensitive)
- `^\s*(\".*[^\\]\").+$` -> `$1`

To force including any string (from a constant definition, for example), 
simply add a comment `// wan24PoeditParser:include` at the end of the line - 
example:

```cs
public const string NAME = "Any PO included keyword";// wan24PoeditParser:include
```

**NOTE**: (Multiline) concatenated string value definitions (like 
`"Part a" + "Part b"`) or interpolations can't be parsed. The matched keyword 
must be C style escaped.

### Poedit extractor configuration

Command to extract translations:

```bash
wan24PoeditParser (-singleThread) (-failOnError) (--config "/path/to/customConfig.json") (--ext ".ext" ...) (-mergeOutput) --output %o %C %F
```

Optional arguments, which will override the default configuration:

| Argument | Description |
| -------- | ----------- |
| `--config` | In case you want to load a custom configuration file |
| `-singleThread`* | Disable multithreading |
| `--ext`* | File extensions to use when walking through a folder tree |
| `-mergeOutput`* | Keywords will be merged to the existing output PO file |
| `-failOnError`* | The whole process will fail on any error |

(*) Will also override the custom configuration, if any

Minimal working example using all default settings:

```bash
wan24PoeditParser --output %o %C %F
```

One input file list entry:

```bash
--input %f
```

Configured source code encoding (default is UTF-8):

```bash
--encoding %c
```

### Custom parser configuration

In the `wan24PoeditParserConfig.json` file in the root folder of this 
repository you find the default configuration. You can download and modify it 
for your needs, and use it with the `--config` parameter in the Poedit 
extractor configuration.

The configuration allows to define regular expressions, where

- an array with two elements is a regular expression (and its `RegexOptions` 
enumeration value) which needs to match the string to use
- an array with three elements is used to replace a pattern (the 3rd element 
is the replacement), if the regular expression does match

Example parser JSON configuration:

```json
{
	"SingleThread": false,// (optional) Set to true to disable multithreading (may be overridden by -singleThread)
	"Encoding": "UTF-8",// (optional) Source encoding to use (default is UTF-8; may be overridden by --encoding)
	"Patterns": [// (optional)
		["Any regular expression", "None"],// Search expression example
		["Any regular search expression", "None", "Replacement"],// Replacement expression example
		...
	],
	"FileExtensions": [// (optional) File extensions to include when walking through a folder tree (may be overridden by --ext)
		".ext",
		...
	],
	"MergeOutput": true,// (optional) Merge the extracted keywords to the existing output PO file
	"FailOnError": true,// (optional) To fail thewhole process on any error
	"Merge": false// (optional) Set to true to merge your custom configuration with the default configuration
}
```

The parser looks for any matching search expression, then applies all matching 
replacement expressions to refer to the keyword to use, finally. If no 
replacement matched the search expression string, the full search match will 
be the used keyword.

During merging, lists will be combined, and single options will be overwritten.

There are some more optional keys:

- `Core`: `wan24-Core` configuration using a `AppConfig` structure
- `CLI`: `wan24-CLI` configuration using a `CliAppConfig` structure

You can find the possible configuration structure in the project repositories 
on GitHub (and their online developer reference also).

### Parsing from the command line

If you want to call the parser manually and use advanced options, you can 
display help like this:

```bash
wan24PoeditParser help (--api API (--method METHOD)) (-details)
```
